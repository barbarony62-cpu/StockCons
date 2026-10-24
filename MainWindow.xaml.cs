using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AskCons;

public partial class MainWindow : Window
{
    private readonly ReadOnlyPortfolio db = new();
    private readonly LocalInterpreter interpreter = new();
    private readonly ObservableCollection<string> questions = new();
    private readonly Dictionary<string, List<string>> saved = new();
    private static readonly string HistoryPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AskCons", "questions.json");
    private bool loadingHistory;
    private bool isAsking;
    private List<TimePoint>? chartPoints;
    private string chartInterval = "month";
    private bool chartShowingTable;
    private object? lastResult;
    private string lastSubtitle = "";
    private string lastNote = "";
    private string lastText = "";
    private string AccountKey => (AccountBox.SelectedItem as Account)?.Id.ToString() ?? "";
    private string DisplayMode => (DisplayModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Automatic";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { Refresh(); LoadHistory(); QuestionBox.Focus(); };
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(HistoryPath));
                if (entries != null) foreach (var item in entries) saved[item.Key] = item.Value;
            }
        }
        catch { /* An invalid local history file must never prevent startup. */ }
        UpdateHistory();
    }
    private void UpdateHistory()
    {
        loadingHistory = true;
        questions.Clear();
        if (saved.TryGetValue(AccountKey, out var list)) foreach (var q in list) questions.Add(q);
        HistoryList.ItemsSource = questions;
        loadingHistory = false;
    }
    private void Remember(string q)
    {
        var key = AccountKey;
        if (string.IsNullOrEmpty(key)) return;
        if (!saved.TryGetValue(key, out var list)) saved[key] = list = new();
        list.RemoveAll(x => string.Equals(x, q, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, q);
        if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HistoryPath)!);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(saved));
        }
        catch { /* History is optional. */ }
        UpdateHistory();
    }
    private void HistorySelected(object sender, SelectionChangedEventArgs e)
    {
        if (loadingHistory || HistoryList.SelectedItem is not string q) return;
        HistoryList.SelectedItem = null;
        QuestionBox.Text = q;
        _ = AskAsync();
    }
    private void QuestionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            _ = AskAsync();
        }
    }
    private void Refresh()
    {
        try
        {
            var old = AccountBox.SelectedValue;
            var accounts = db.Accounts();
            AccountBox.ItemsSource = accounts;
            AccountBox.SelectedItem = accounts.FirstOrDefault(x => Equals(x.Id, old)) ?? accounts.FirstOrDefault();
        }
        catch (Exception e) { ShowText("Unable to read Consolidation data: " + e.Message); }
    }
    private void AccountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnswerText == null || AccountBox.SelectedItem is not Account a) return;
        ShowText($"Selected account: {a.Name}. Ask a question below.");
        UpdateHistory();
    }
    private async void TestAiClick(object sender, RoutedEventArgs e)
    {
        AiStatus.Text = "Testing local Ollama...";
        try { AiStatus.Text = await interpreter.TestConnection(CancellationToken.None); }
        catch (Exception ex) { AiStatus.Text = "AI connection failed: " + ex.Message; }
    }
    private async void AskClick(object sender, RoutedEventArgs e) => await AskAsync();
    private void DisplayModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnswerText == null || isAsking || lastResult == null) return;
        RenderResult(lastResult, lastSubtitle, lastNote, lastText);
    }
    private async Task AskAsync()
    {
        if (isAsking) { AiStatus.Text = "A question is already processing."; return; }
        if (AccountBox.SelectedItem is not Account account) { ShowText("Select an account first."); return; }
        var question = QuestionBox.Text.Trim();
        if (question.Length == 0) return;
        Remember(question);
        isAsking = true;
        AskButton.IsEnabled = false;
        AskButton.Content = "Working...";
        ShowText("Processing your question...");
        try
        {
            var intent = await interpreter.Interpret(question, CancellationToken.None);
            var result = await Task.Run(() => Calculate(account, QueryValidator.Validate(intent), question));
            RenderResult(result.Data, result.Subtitle, result.Note, result.Text);
        }
        catch (Exception ex) { ShowText("Question failed. Please retry or test the AI connection.\n\n" + ex.Message); }
        finally
        {
            isAsking = false;
            AskButton.IsEnabled = true;
            AskButton.Content = "Ask →";
            QuestionBox.Focus();
        }
    }

    private sealed record AnswerData(object Data, string Subtitle, string Note, string Text);
    private static DateTime? ParseDate(string? s) => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private static bool LooksLikeRanking(string q, out bool worst)
    {
        q = q.ToLowerInvariant();
        worst = q.Contains("worst") || q.Contains("bottom") || q.Contains("losers") || q.Contains("גרוע") || q.Contains("הפסד");
        return (worst || q.Contains("best") || q.Contains("top") || q.Contains("gainers") || q.Contains("biggest") || q.Contains("הכי טוב")) &&
            (q.Contains("stock") || q.Contains("holding") || q.Contains("securit") || q.Contains("share") || q.Contains("מני") || q.Contains("נייר"));
    }
    private static bool LooksLikeSeries(string q, out string interval)
    {
        q = q.ToLowerInvariant();
        interval = q.Contains("week") || q.Contains("שבוע") ? "week" : q.Contains("day by day") || q.Contains("daily") || q.Contains("יומי") ? "day" : "month";
        return q.Contains("by month") || q.Contains("monthly") || q.Contains("each month") || q.Contains("by week") || q.Contains("weekly") || q.Contains("each week") || q.Contains("graph") || q.Contains("chart") || q.Contains("plot") || q.Contains("trend") || q.Contains("לפי חודש") || q.Contains("חודשי") || q.Contains("לפי שבוע") || q.Contains("שבועי") || q.Contains("גרף");
    }
    private AnswerData Calculate(Account account, QueryIntent intent, string question)
    {
        if (intent.operation == "unsupported") return Plain("I cannot safely calculate this request: " + (string.IsNullOrWhiteSpace(intent.unsupported_reason) ? "A requested operation is not supported by the local data adapter." : intent.unsupported_reason));
        var yearMatch = Regex.Match(question, @"\b(?:19|20)\d{2}\b");
        var year = yearMatch.Success ? int.Parse(yearMatch.Value, CultureInfo.InvariantCulture) : DateTime.Today.Year;
        var end = ParseDate(intent.end_date) ?? (year < DateTime.Today.Year ? new DateTime(year, 12, 31) : DateTime.Today);
        var start = ParseDate(intent.start_date) ?? new DateTime(year, 1, 1);
        if (yearMatch.Success && year < DateTime.Today.Year && end.Year != year) end = new DateTime(year, 12, 31);
        if (end > DateTime.Today || start > end) return Plain("Invalid or future date range.");

        if (intent.operation is "top_holding_changes" or "bottom_holding_changes" or "holdings_filter" or "holdings_list" or "holdings_disappeared"  )
        {
            var worst = intent.operation == "bottom_holding_changes";
            var q = question.ToLowerInvariant();
            bool openingOnly = intent.present_at_start == true || intent.operation == "holdings_disappeared" || q.Contains("first recorded") || q.Contains("first day") || q.Contains("beginning of") || q.Contains("start of") || q.Contains("already held") || q.Contains("existed on") || q.Contains("existing at") || q.Contains("תחילת") || q.Contains("יום הראשון");
            if (openingOnly && (yearMatch.Success || q.Contains("year") || q.Contains("שנה")))
            {
                var first = db.FirstSnapshotDate(account.Id, new DateTime(year, 1, 1), new DateTime(year, 12, 31));
                if (first == null) return Plain($"No saved snapshots were found in {year}.");
                start = first.Value;
            }
            var ranking = db.RankHoldingChanges(account.Id, start, end, openingOnly);
            if (ranking == null) return Plain("No saved snapshots are available for both dates.");
            var countMatch = Regex.Match(question, @"\b(?:top|best|worst|bottom)\s+(\d{1,2})\b", RegexOptions.IgnoreCase);
            int count = Math.Clamp(intent.limit ?? (countMatch.Success ? int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture) : (intent.operation is "holdings_list" or "holdings_filter" or "holdings_disappeared" ? 100 : 5)), 1, 100);
            // The database has no authoritative security-type classification in this read-only adapter.
            // Reject the request instead of silently returning funds for a stocks-only request.
            if (intent.security_type is "stock" or "fund" or "bond")
                return Plain("A reliable stock/fund/bond classification is not available from the current read-only holding fields. I cannot safely apply that filter yet. Ask for 'holdings' or 'securities' instead.");
            IEnumerable<HoldingChange> filtered = ranking.Items;
            if (intent.operation == "holdings_disappeared" || intent.absent_at_end == true)
                filtered = filtered.Where(x => x.EndValue == 0 && x.StartValue != 0);
            if (intent.minimum_change_nis.HasValue)
                filtered = filtered.Where(x => x.Change >= intent.minimum_change_nis.Value);
            if (intent.maximum_change_nis.HasValue)
                filtered = filtered.Where(x => x.Change <= intent.maximum_change_nis.Value);
            if (intent.require_present_at_end == true) filtered = filtered.Where(x => x.EndValue != 0);
            if (intent.minimum_start_value_nis.HasValue) filtered = filtered.Where(x => x.StartValue >= intent.minimum_start_value_nis.Value);
            if (intent.maximum_start_value_nis.HasValue) filtered = filtered.Where(x => x.StartValue <= intent.maximum_start_value_nis.Value);
            if (intent.minimum_end_value_nis.HasValue) filtered = filtered.Where(x => x.EndValue >= intent.minimum_end_value_nis.Value);
            if (intent.maximum_end_value_nis.HasValue) filtered = filtered.Where(x => x.EndValue <= intent.maximum_end_value_nis.Value);
            if (!string.IsNullOrWhiteSpace(intent.name_contains)) filtered = filtered.Where(x => x.Name.Contains(intent.name_contains, StringComparison.OrdinalIgnoreCase) || x.Symbol.Contains(intent.name_contains, StringComparison.OrdinalIgnoreCase));
            if (intent.direction == "gain") filtered = filtered.Where(x => x.Change > 0);
            if (intent.direction == "loss") filtered = filtered.Where(x => x.Change < 0);
            if (intent.operation == "holdings_list") filtered = filtered.OrderByDescending(x => x.EndValue);
            else if (intent.sort_by == "name") filtered = filtered.OrderBy(x => x.Name);
            else if (intent.sort_by == "end_value") filtered = filtered.OrderByDescending(x => x.EndValue);
            else if (intent.sort_by == "start_value") filtered = filtered.OrderByDescending(x => x.StartValue);
            else filtered = worst ? filtered.OrderBy(x => x.Change) : filtered.OrderByDescending(x => x.Change);
            if (intent.sort_direction == "asc" && !worst && intent.sort_by is "start_value" or "end_value" or "name") filtered = intent.sort_by switch
            {
                "start_value" => filtered.OrderBy(x => x.StartValue),
                "end_value" => filtered.OrderBy(x => x.EndValue),
                _ => filtered.OrderByDescending(x => x.Name)
            };
            var rows = filtered.Take(count).Select((x, i) => new RankingRow { Rank = i + 1, Security = x.Name + (string.IsNullOrWhiteSpace(x.Symbol) ? "" : " (" + x.Symbol + ")"), Start = x.StartValue, End = x.EndValue, Change = x.Change }).ToList();
            if (rows.Count == 0) return Plain("No holdings qualify for the selected period and opening-date restriction.");
            string subtitle = $"{account.Name}  ·  {ranking.StartDate:dd MMM yyyy} – {ranking.EndDate:dd MMM yyyy}  ·  NIS";
            string note = (openingOnly ? "Only securities present in the opening snapshot.  " : "") + "Recorded holding-value changes; purchases and sales are not adjusted. Includes funds and bonds where recorded.";
            string text = string.Join("\n", rows.Select(x => $"{x.Rank}. {x.Security}: {x.Start:N2} → {x.End:N2} NIS; change {x.Change:+#,##0.00;-#,##0.00;0.00} NIS"));
            return new AnswerData(rows, subtitle, note, text);
        }

        var interval = intent.interval ?? "month";
        bool seriesRequested = intent.operation is "holding_series" or "account_series";
        if (seriesRequested)
        {
            interval = intent.interval is "week" or "day" ? intent.interval : "month";
            bool accountSeries = intent.operation == "account_series" || (intent.operation != "holding_series" && (question.Contains("portfolio", StringComparison.OrdinalIgnoreCase) || question.Contains("account", StringComparison.OrdinalIgnoreCase) || question.Contains("חשבון")));
            var security = accountSeries ? null : intent.security?.Trim();
            if (!accountSeries && string.IsNullOrWhiteSpace(security)) return Plain("Which security should I graph? Please include its name or symbol.");
            var data = db.HistoricalSeries(account.Id, security, start, end, interval);
            if (data == null || data.Points.Count == 0) return Plain("No historical snapshots are available for that period.");
            var subtitle = $"{account.Name}  ·  {data.Label}  ·  {interval}ly recorded values in NIS  ·  {data.Points.First().Date:dd MMM yyyy} – {data.Points.Last().Date:dd MMM yyyy}";
            var note = "Each point is the latest saved valuation in that period; missing periods are not invented. Hover over a point to see its date and value.";
            var text = string.Join("\n", data.Points.Select(x => $"{x.Date:yyyy-MM-dd}: {x.Value:N2} NIS"));
            return new AnswerData(data, subtitle, note, text);
        }

        var op = intent.operation;
        if (op == "holding_share")
        {
            var security = intent.security?.Trim();
            if (string.IsNullOrWhiteSpace(security)) return Plain("Which security do you mean?");
            var holdingValue = db.HoldingValue(account.Id, security, end);
            var accountValue = db.AccountValue(account.Id, end);
            if (holdingValue == null || accountValue == null || accountValue.Value == 0)
                return Plain("Cannot calculate the holding's share: a matching security or account valuation is unavailable.");
            return Plain($"{account.Name} · {security}\nHolding: {holdingValue.Value:N2} NIS ({holdingValue.Date:yyyy-MM-dd})\nAccount: {accountValue.Value:N2} NIS ({accountValue.Date:yyyy-MM-dd})\nShare: {holdingValue.Value / accountValue.Value:P2}\nNote: valuations may be from different snapshot dates; confirm dates before comparing.");
        }
        if (op is not ("account_change" or "account_value" or "holding_change" or "holding_value")) return Plain("I cannot reliably calculate that question yet. Try specifying the security, period, and metric.");
        bool holding = op.StartsWith("holding_", StringComparison.Ordinal);
        var term = intent.security?.Trim();
        if (holding && string.IsNullOrWhiteSpace(term)) return Plain("Please specify the security name or symbol.");
        var current = holding ? db.HoldingValue(account.Id, term!, end) : db.AccountValue(account.Id, end);
        if (current == null) return Plain("No matching saved valuation was found.");
        var label = holding ? term! : "Account";
        if (op.EndsWith("_value", StringComparison.Ordinal)) return Plain($"{account.Name} · {label}\n{current.Date:yyyy-MM-dd}: {current.Value:N2} NIS");
        var from = ParseDate(intent.start_date) ?? start;
        var previous = holding ? db.HoldingValue(account.Id, term!, from) : db.AccountValue(account.Id, from);
        if (previous == null) return Plain("No opening valuation was found. No financial value was estimated.");
        var diff = current.Value - previous.Value;
        return Plain($"{account.Name} · {label}\nStart ({previous.Date:yyyy-MM-dd}): {previous.Value:N2} NIS\nEnd ({current.Date:yyyy-MM-dd}): {current.Value:N2} NIS\nChange: {diff:+#,##0.00;-#,##0.00;0.00} NIS");
    }
    private static AnswerData Plain(string text) => new(text, "", "", text);

    private void ShowText(string text)
    {
        lastResult = null;
        AnswerHeading.Text = "Ask about your investments";
        AnswerSubtitle.Text = "";
        AnswerNote.Text = "";
        AnswerText.Text = text;
        AnswerText.Visibility = Visibility.Visible;
        AnswerScroll.Visibility = Visibility.Visible;
        ResultsGrid.Visibility = Visibility.Collapsed;
        ChartHost.Visibility = Visibility.Collapsed;
        ShowDataButton.Visibility = Visibility.Collapsed;
        AnswerScroll.ScrollToTop();
    }
    private void RenderResult(object result, string subtitle, string note, string text)
    {
        lastResult = result; lastSubtitle = subtitle; lastNote = note; lastText = text;
        AnswerSubtitle.Text = subtitle;
        AnswerNote.Text = note;
        AnswerText.Text = text;
        AnswerScroll.ScrollToTop();
        AnswerScroll.Visibility = Visibility.Collapsed;
        ResultsGrid.Visibility = Visibility.Collapsed;
        ChartHost.Visibility = Visibility.Collapsed;
        ShowDataButton.Visibility = Visibility.Collapsed;
        chartPoints = null;
        var mode = DisplayMode;
        if (result is List<RankingRow> rows)
        {
            AnswerHeading.Text = "Holdings ranked by change in value";
            if (mode == "Text") { AnswerScroll.Visibility = Visibility.Visible; return; }
            ConfigureRankingGrid(rows);
            ResultsGrid.Visibility = Visibility.Visible;
        }
        else if (result is SeriesResult series)
        {
            AnswerHeading.Text = "Historical value — " + series.Label;
            chartInterval = series.Interval;
            chartPoints = series.Points;
            if (mode == "Text") { AnswerScroll.Visibility = Visibility.Visible; return; }
            if (mode == "Table") { ConfigureSeriesGrid(series); ResultsGrid.Visibility = Visibility.Visible; }
            else { ChartHost.Visibility = Visibility.Visible; chartShowingTable = false; ShowDataButton.Content = "Show data table"; ShowDataButton.Visibility = Visibility.Visible; Dispatcher.BeginInvoke(new Action(DrawChart), System.Windows.Threading.DispatcherPriority.Loaded); }
        }
        else
        {
            AnswerHeading.Text = "Answer";
            AnswerScroll.Visibility = Visibility.Visible;
            if (mode == "Graph" || mode == "Table") AnswerNote.Text = "A graph or table is not meaningful for this single-value answer. " + note;
        }
    }
    private static DataGridTextColumn Column(string title, string path, string? format = null, double width = 1)
    {
        var binding = new Binding(path);
        if (format != null) binding.StringFormat = format;
        return new DataGridTextColumn { Header = title, Binding = binding, Width = new DataGridLength(width, DataGridLengthUnitType.Star), IsReadOnly = true };
    }
    private void ConfigureRankingGrid(List<RankingRow> rows)
    {
        ResultsGrid.Columns.Clear();
        ResultsGrid.Columns.Add(Column("#", nameof(RankingRow.Rank), null, 0.4));
        ResultsGrid.Columns.Add(Column("Security", nameof(RankingRow.Security), null, 3));
        ResultsGrid.Columns.Add(Column("Start (₪)", nameof(RankingRow.Start), "N2", 1.35));
        ResultsGrid.Columns.Add(Column("End (₪)", nameof(RankingRow.End), "N2", 1.35));
        ResultsGrid.Columns.Add(Column("Change (₪)", nameof(RankingRow.Change), "+#,##0.00;-#,##0.00;0.00", 1.5));
        ResultsGrid.ItemsSource = rows;
    }
    private void ConfigureSeriesGrid(SeriesResult series)
    {
        ResultsGrid.Columns.Clear();
        ResultsGrid.Columns.Add(Column("Period", nameof(SeriesRow.Period), null, 1.2));
        ResultsGrid.Columns.Add(Column("Actual snapshot", nameof(SeriesRow.Date), "yyyy-MM-dd", 1.3));
        ResultsGrid.Columns.Add(Column("Value (₪)", nameof(SeriesRow.Value), "N2", 1.6));
        ResultsGrid.ItemsSource = series.Points.Select(p => new SeriesRow { Period = series.Interval == "week" ? "Week ending " + p.Date.AddDays((6 - (int)p.Date.DayOfWeek + 7) % 7).ToString("yyyy-MM-dd") : series.Interval == "day" ? p.Date.ToString("yyyy-MM-dd") : p.Date.ToString("yyyy-MM"), Date = p.Date, Value = p.Value }).ToList();
    }
    private void ToggleDataClick(object sender, RoutedEventArgs e)
    {
        if (lastResult is not SeriesResult series) return;
        chartShowingTable = !chartShowingTable;
        if (chartShowingTable)
        {
            ConfigureSeriesGrid(series);
            ResultsGrid.Visibility = Visibility.Visible;
            ChartHost.Visibility = Visibility.Collapsed;
            ShowDataButton.Content = "Show graph";
        }
        else
        {
            ResultsGrid.Visibility = Visibility.Collapsed;
            ChartHost.Visibility = Visibility.Visible;
            ShowDataButton.Content = "Show data table";
            Dispatcher.BeginInvoke(new Action(DrawChart), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
    private void ChartSizeChanged(object sender, SizeChangedEventArgs e) { if (ChartHost.Visibility == Visibility.Visible) DrawChart(); }
    private void DrawChart()
    {
        PlotCanvas.Children.Clear();
        if (chartPoints is not { Count: > 0 }) return;
        var w = ChartHost.ActualWidth; var h = ChartHost.ActualHeight;
        if (w < 200 || h < 150) return;
        const double left = 88, right = 30, top = 32, bottom = 68;
        var pw = Math.Max(1, w - left - right); var ph = Math.Max(1, h - top - bottom);
        var min = chartPoints.Min(x => x.Value); var max = chartPoints.Max(x => x.Value);
        var pad = Math.Max((max - min) * 0.12, Math.Max(Math.Abs(max) * 0.04, 1));
        min -= pad; max += pad;
        double X(int index) => left + (chartPoints.Count == 1 ? pw / 2 : index * pw / (chartPoints.Count - 1));
        double Y(double value) => top + (max - value) * ph / (max - min);
        for (var tick = 0; tick <= 4; tick++)
        {
            var value = min + (max - min) * tick / 4;
            var y = Y(value);
            PlotCanvas.Children.Add(new Line { X1 = left, X2 = w - right, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(225, 234, 245)), StrokeThickness = 1 });
            AddLabel(value.ToString("N0", CultureInfo.InvariantCulture), 0, y - 10, 80, 12, Brushes.SlateGray);
        }
        var line = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(9, 93, 181)), StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
        for (int i = 0; i < chartPoints.Count; i++) line.Points.Add(new Point(X(i), Y(chartPoints[i].Value)));
        PlotCanvas.Children.Add(line);
        var step = Math.Max(1, (int)Math.Ceiling(chartPoints.Count / 7.0));
        for (int i = 0; i < chartPoints.Count; i++)
        {
            var p = chartPoints[i]; var x = X(i); var y = Y(p.Value);
            var marker = new Ellipse { Width = 9, Height = 9, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(9, 93, 181)), StrokeThickness = 2, ToolTip = $"{p.Date:yyyy-MM-dd}\n{p.Value:N2} NIS" };
            Canvas.SetLeft(marker, x - 4.5); Canvas.SetTop(marker, y - 4.5); PlotCanvas.Children.Add(marker);
            if (i % step == 0 || i == chartPoints.Count - 1)
            {
                var label = chartInterval == "month" ? p.Date.ToString("MMM yy", CultureInfo.InvariantCulture) : p.Date.ToString("dd MMM", CultureInfo.InvariantCulture);
                AddLabel(label, x - 35, h - bottom + 15, 70, 12, Brushes.SlateGray);
            }
        }
        AddLabel("NIS", 5, 5, 60, 13, Brushes.SlateGray);
    }
    private void AddLabel(string text, double x, double y, double width, double size, Brush color)
    {
        var label = new TextBlock { Text = text, Width = width, FontSize = size, Foreground = color, TextAlignment = TextAlignment.Center };
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y); PlotCanvas.Children.Add(label);
    }
}
