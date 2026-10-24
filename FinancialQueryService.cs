using System.Globalization;
using System.Text.RegularExpressions;
namespace AskCons;
public sealed record AnswerData(object Data, string Subtitle, string Note, string Text, bool Success = true);
public sealed class FinancialQueryService(ReadOnlyPortfolio db) {
    public static string Describe(string operation) => operation switch {
        "account_value" => "Account value", "account_change" => "Account value change",
        "holding_value" => "Holding value", "holding_change" => "Holding value change",
        "holding_share" => "Share of account", "holding_series" => "Holding history",
        "account_series" => "Account history", "holdings_disappeared" => "Holdings no longer present",
        _ => "Holdings comparison"
    };
    private static DateTime? ParseDate(string? s) => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    public AnswerData Calculate(Account account, QueryIntent intent)
    {
        if (intent.operation == "unsupported") return Failure("I cannot safely calculate this request: " + (string.IsNullOrWhiteSpace(intent.unsupported_reason) ? "A requested operation is not supported by the local data adapter." : intent.unsupported_reason));
        QueryValidator.Validate(intent);
        var start = ParseDate(intent.start_date)!.Value;
        var end = ParseDate(intent.end_date)!.Value;
        if (intent.operation is "top_holding_changes" or "bottom_holding_changes" or "holdings_filter" or "holdings_list" or "holdings_disappeared"  )
        {
            var worst = intent.operation == "bottom_holding_changes";
            bool openingOnly = intent.present_at_start == true || intent.operation == "holdings_disappeared";
            if (intent.first_recorded_start) {
                var first = db.FirstSnapshotDate(account.Id, start, end);
                if (first == null) return Failure("No snapshots exist in the requested period.");
                start = first.Value;
            }
            var ranking = db.RankHoldingChanges(account.Id, start, end, openingOnly);
            if (ranking == null) return Failure("No saved snapshots are available for both dates.");
            int count = intent.limit ?? (intent.operation is "holdings_list" or "holdings_filter" or "holdings_disappeared" ? 100 : 5);
            // The database has no authoritative security-type classification in this read-only adapter.
            // Reject the request instead of silently returning funds for a stocks-only request.
            if (intent.security_type is "stock" or "fund" or "bond")
                return Failure("A reliable stock/fund/bond classification is not available from the current read-only holding fields. I cannot safely apply that filter yet. Ask for 'holdings' or 'securities' instead.");
            IEnumerable<HoldingChange> filtered = ranking.Items;
            if (intent.operation == "holdings_disappeared" || intent.absent_at_end == true)
                filtered = filtered.Where(x => x.PresentAtStart && !x.PresentAtEnd);
            if (intent.minimum_change_nis.HasValue)
                filtered = filtered.Where(x => x.Change >= intent.minimum_change_nis.Value);
            if (intent.maximum_change_nis.HasValue)
                filtered = filtered.Where(x => x.Change <= intent.maximum_change_nis.Value);
            if (intent.require_present_at_end == true) filtered = filtered.Where(x => x.PresentAtEnd);
            if (intent.minimum_start_value_nis.HasValue) filtered = filtered.Where(x => x.StartValue >= intent.minimum_start_value_nis.Value);
            if (intent.maximum_start_value_nis.HasValue) filtered = filtered.Where(x => x.StartValue <= intent.maximum_start_value_nis.Value);
            if (intent.minimum_end_value_nis.HasValue) filtered = filtered.Where(x => x.EndValue >= intent.minimum_end_value_nis.Value);
            if (intent.maximum_end_value_nis.HasValue) filtered = filtered.Where(x => x.EndValue <= intent.maximum_end_value_nis.Value);
            if (!string.IsNullOrWhiteSpace(intent.name_contains)) filtered = filtered.Where(x => x.Name.Contains(intent.name_contains, StringComparison.OrdinalIgnoreCase) || x.Symbol.Contains(intent.name_contains, StringComparison.OrdinalIgnoreCase));
            if (intent.direction == "gain") filtered = filtered.Where(x => x.Change > 0);
            if (intent.direction == "loss") filtered = filtered.Where(x => x.Change < 0);
            var sort = intent.sort_by ?? (intent.operation == "holdings_list" ? "end_value" : "change");
            bool asc = intent.sort_direction == "asc" || (intent.sort_direction == null && (worst || sort == "name"));
            filtered = sort switch {
                "name" => asc ? filtered.OrderBy(x=>x.Name) : filtered.OrderByDescending(x=>x.Name),
                "end_value" => asc ? filtered.OrderBy(x=>x.EndValue) : filtered.OrderByDescending(x=>x.EndValue),
                "start_value" => asc ? filtered.OrderBy(x=>x.StartValue) : filtered.OrderByDescending(x=>x.StartValue),
                _ => asc ? filtered.OrderBy(x=>x.Change) : filtered.OrderByDescending(x=>x.Change)
            };
            var rows = filtered.Take(count).Select((x, i) => new RankingRow { Rank = i + 1, Security = x.Name, SecurityKey = string.IsNullOrWhiteSpace(x.Symbol) ? x.Name : x.Symbol, Start = x.StartValue, End = x.EndValue, Change = x.Change }).ToList();
            if (rows.Count == 0) return Failure("No holdings qualify for the selected period and opening-date restriction.");
            string subtitle = $"{account.Name}  ·  {ranking.StartDate:dd MMM yyyy} – {ranking.EndDate:dd MMM yyyy}  ·  NIS";
            var sortLabel = sort switch { "change" => "recorded value change", "start_value" => "opening holding value", "end_value" => "ending holding value", _ => "security name" };
            string note = $"Sorted by {sortLabel}, {(asc ? "ascending" : "descending")}. " + (openingOnly ? "Only securities present in the opening snapshot.  " : "") + "Recorded holding-value changes; purchases and sales are not adjusted. Includes all recorded security types (stocks, funds and bonds); stock-only classification is not available.";
            string text = string.Join("\n", rows.Select(x => $"{x.Rank}. {x.Security}: {x.Start:N2} → {x.End:N2} NIS; change {x.Change:+#,##0.00;-#,##0.00;0.00} NIS"));
            return new AnswerData(rows, subtitle, note, text);
        }

        var interval = intent.interval ?? "month";
        bool seriesRequested = intent.operation is "holding_series" or "account_series";
        if (seriesRequested)
        {
            interval = intent.interval is "week" or "day" ? intent.interval : "month";
            bool accountSeries = intent.operation == "account_series";
            var security = accountSeries ? null : intent.security?.Trim();
            if (!accountSeries && string.IsNullOrWhiteSpace(security)) return Failure("Which security should I graph? Please include its name or symbol.");
            var data = db.HistoricalSeries(account.Id, security, start, end, interval);
            if (data == null || data.Points.Count == 0) return Failure("No historical snapshots are available for that period.");
            var subtitle = $"{account.Name}  ·  {data.Label}  ·  {interval}ly recorded values in NIS  ·  {data.Points.First().Date:dd MMM yyyy} – {data.Points.Last().Date:dd MMM yyyy}";
            var note = "Each point is the latest saved valuation in that period; missing periods are not invented. Hover over a point to see its date and value.";
            var text = string.Join("\n", data.Points.Select(x => $"{x.Date:yyyy-MM-dd}: {x.Value:N2} NIS"));
            return new AnswerData(data, subtitle, note, text);
        }

        var op = intent.operation;
        if (op == "holding_share")
        {
            var security = intent.security?.Trim();
            if (string.IsNullOrWhiteSpace(security)) return Failure("Which security do you mean?");
            var pair = db.HoldingShare(account.Id, security, end);
            var accountValue = pair?.Account; var holdingValue = pair?.Holding;
            if (holdingValue == null || accountValue == null || accountValue.Value == 0)
                return Failure("Cannot calculate the holding's share: a matching security or account valuation is unavailable.");
            return Plain($"{account.Name} · {security}\nHolding: {holdingValue.Value:N2} NIS ({holdingValue.Date:yyyy-MM-dd})\nAccount: {accountValue.Value:N2} NIS ({accountValue.Date:yyyy-MM-dd})\nShare: {holdingValue.Value / accountValue.Value:P2}\nNote: holding and account values use the same snapshot date.");
        }
        if (op is not ("account_change" or "account_value" or "holding_change" or "holding_value")) return Failure("I cannot reliably calculate that question yet. Try specifying the security, period, and metric.");
        bool holding = op.StartsWith("holding_", StringComparison.Ordinal);
        var term = intent.security?.Trim();
        if (holding && string.IsNullOrWhiteSpace(term)) return Failure("Please specify the security name or symbol.");
        var current = holding ? db.HoldingValue(account.Id, term!, end) : db.AccountValue(account.Id, end);
        if (current == null) return Failure("No matching saved valuation was found.");
        var label = holding ? term! : "Account";
        if (op.EndsWith("_value", StringComparison.Ordinal)) return Plain($"{account.Name} · {label}\n{current.Date:yyyy-MM-dd}: {current.Value:N2} NIS");
        var from = ParseDate(intent.start_date) ?? start;
        var previous = holding ? db.HoldingValue(account.Id, term!, from) : db.AccountValue(account.Id, from);
        if (previous == null) return Failure("No opening valuation was found. No financial value was estimated.");
        var diff = current.Value - previous.Value;
        var percent = previous.Value > 0 ? (diff / previous.Value).ToString("+0.00%;-0.00%;0.00%") : "not meaningful with a nonpositive opening value";
        return Plain($"{account.Name} · {label}\nStart ({previous.Date:yyyy-MM-dd}): {previous.Value:N2} NIS\nEnd ({current.Date:yyyy-MM-dd}): {current.Value:N2} NIS\nChange: {diff:+#,##0.00;-#,##0.00;0.00} NIS\nPercentage value change: {percent}\nRecorded value change only; purchases, sales and income are not adjusted.");
    }
    private static AnswerData Failure(string text) => new(text, "", "", text, false);
    private static AnswerData Plain(string text) => new(text, "", "", text);

}








