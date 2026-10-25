using System.Globalization;
namespace AskCons;
public static class OwnershipQueries
{
    public static bool IsOwnership(string operation) => operation is "holding_first_owned" or "holding_history" or "holding_quantity_history";
    public static AnswerData Calculate(ReadOnlyPortfolio db,Account account,QueryIntent plan) {
        QueryValidator.Validate(plan);
        var matches=db.ResolveSecurity(account.Id,plan.security!);
        if(matches.Count!=1) return Fail(matches.Count==0?"No matching security is recorded in this account.":"The security name is ambiguous. Please specify its exact ticker.");
        var security=matches[0];
        var from=Parse(plan.start_date); var to=Parse(plan.end_date);
        if(plan.operation=="holding_first_owned") {
            var events=db.StoredHistory(account.Id,security).Where(x=>x.EventType.Equals("First owned",StringComparison.OrdinalIgnoreCase)).ToList();
            if(events.Count==0) return Fail($"{security}: no explicit 'First owned' record exists for {account.Name}. Earliest snapshots and quantity observations do not establish a purchase date.");
            var first=events[0];
            var period=Period(first.FromDate,first.ToDate);
            var source=string.IsNullOrWhiteSpace(first.Source)?"Consolidation stored holding history":first.Source;
            var text=$"{security} — Recorded first owned: {period}\nAccount: {account.Name}\nEvent: First owned\nSource: {source}\nThis is a recorded ownership date, not proof of an actual purchase transaction.";
            if(events.Select(e=>(e.FromDate,e.ToDate)).Distinct().Count()>1) text+="\nMultiple first-owned dates are recorded; the earliest event is shown.";
            return new(text,$"{account.Name} · {security}","Read from Consolidation's stored holding history; no date inferred from snapshots.",text);
        }
        var rows=new List<HistoryRow>();
        if(plan.operation=="holding_history") {
            var stored=db.StoredHistory(account.Id,security,from,to);
            rows.AddRange(stored.Select(e=>new HistoryRow {EventDate=e.ToDate,Period=Period(e.FromDate,e.ToDate),Event=e.EventType,
                Quantity=Quantity(e),Source=string.IsNullOrWhiteSpace(e.Source)?"Stored holding history":e.Source}));
        }
        var observed=db.ObservedQuantities(account.Id,security,from,to);
        rows.AddRange(observed.Select(e=>new HistoryRow {EventDate=e.Date,Period=e.Date.ToString("dd MMM yyyy",CultureInfo.InvariantCulture),
            Event="Quantity observed in snapshot",Quantity=e.Quantity.ToString("N4"),Source="Consolidation portfolio snapshot"}));
        if(rows.Count==0) return Fail($"No {(plan.operation=="holding_quantity_history"?"snapshot quantity observations":"holding history records")} were found for {security} in this account and period.");
        rows=rows.OrderBy(x=>x.EventDate).ToList();
        var textRows=string.Join("\n",rows.Select(e=>$"{e.Period} · {e.Event} · Quantity: {e.Quantity} · Source: {e.Source}"));
        var note="Stored events retain their source labels. Snapshot quantities are observations; changes are not automatically purchases, sales or stock splits.";
        return new(rows,$"{account.Name} · {security} · {from?.ToString("yyyy-MM-dd")??"all recorded history"} – {to?.ToString("yyyy-MM-dd")??"latest records"}",note,textRows);
    }
    private static DateTime? Parse(string? value)=>value==null?null:DateTime.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
    private static string Period(DateTime from,DateTime to)=>from==to?to.ToString("dd MMM yyyy",CultureInfo.InvariantCulture):$"{from:dd MMM yyyy} – {to:dd MMM yyyy}";
    private static string Quantity(RecordedHistoryEvent e)=>e.EventType.Equals("First owned",StringComparison.OrdinalIgnoreCase)?"—":
        e.FromQuantity.HasValue&&e.ToQuantity.HasValue?$"{e.FromQuantity.Value:N4} → {e.ToQuantity.Value:N4}":
        e.ToQuantity?.ToString("N4")??e.FromQuantity?.ToString("N4")??"—";
    private static AnswerData Fail(string text)=>new(text,"","",text,false);
}

