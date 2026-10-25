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
    private string preferredPresentation = "auto";
    private bool loadingHistory;
    private readonly Dictionary<long, ConversationSession> sessions = new();
    private ConversationSession Session(long id) { if (!sessions.TryGetValue(id, out var s)) sessions[id] = s = new(); return s; }
    private CancellationTokenSource? requestCancellation;
    private int conversationGeneration;
    private void CancelClick(object sender, RoutedEventArgs e) => requestCancellation?.Cancel();
    private void NewConversationClick(object sender, RoutedEventArgs e) {
        requestCancellation?.Cancel(); conversationGeneration++;
        if (AccountBox.SelectedItem is Account a) sessions[a.Id] = new();
        Transcript.Children.Clear(); ShowText("New conversation. Ask a question.");
    }
    private void AddTurn(ConversationSession session, string role, string text) {
        session.Turns.Add((role,text));
        if (session.Turns.Count > 100) session.Turns.RemoveAt(0);
        AddBubble(role,text);
    }
    private void AddBubble(string role,string text) {
        Transcript.Children.Add(new Border { Background = role == "You" ? Brushes.AliceBlue : Brushes.WhiteSmoke,
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0,0,0,8),
            Child = new TextBlock { Text = role + ": " + text, TextWrapping = TextWrapping.Wrap, FontSize=15 } });
        if (Transcript.Children.Count > 100) Transcript.Children.RemoveAt(0);
        ConversationScroll.ScrollToEnd();
    }
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
        Closed += (_, _) => { requestCancellation?.Cancel(); conversationGeneration++; };
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
        requestCancellation?.Cancel(); conversationGeneration++;
        Transcript.Children.Clear();
        foreach (var turn in Session(a.Id).Turns) AddBubble(turn.Role, turn.Text);
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
        if (isAsking) return;
        if (AccountBox.SelectedItem is not Account account) { ShowText("Select an account first."); return; }
        var question = QuestionBox.Text.Trim();
        if (question.Length == 0) return;
        var session = Session(account.Id);
        session.SelectedAccount = account.Name;
        int generation = conversationGeneration;
        Remember(question);
        AddTurn(session, "You", question);
        QuestionBox.Clear();
        isAsking = true;
        AskButton.IsEnabled = false; CancelButton.IsEnabled = true;
        AskButton.Content = "Working...";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        requestCancellation = cancellation;
        var token = cancellation.Token;
        bool Active() => generation == conversationGeneration && !token.IsCancellationRequested;
        void Reply(string text) { AddTurn(session, "AskCons", text); ShowText(text); }
        try
        {
            if (QuestionPolicy.NeedsMetricClarification(question)) {
                session.PendingQuestion = question;
                Reply(QuestionPolicy.MetricQuestion);
                return;
            }
            var intent = await interpreter.Interpret(question, session, token);
            token.ThrowIfCancellationRequested();
            if (!Active()) return;
            if (intent.action == "clarify") {
                session.PendingQuestion ??= question;
                Reply(intent.clarification ?? "Which holding, period and metric do you mean?");
                return;
            }
            if (intent.action == "explain") {
                Reply(session.LastPlan == null ? "Ask a financial question first." :
                    session.LastAnswer + (OwnershipQueries.IsOwnership(session.LastPlan.operation) ? "\n\nSource: stored ownership events retain their recorded event labels; snapshot quantities are observations, not proof of purchases or sales." : "\n\nCalculation: recorded snapshot values in NIS. Change is end minus start; purchases, sales, income and currency effects are not separated. Series use the last saved observation in each period."));
                return;
            }
            if (intent.action == "unsupported" || intent.operation == "unsupported") {
                Reply("This request is not available: " + (intent.unsupported_reason ?? "Please specify a recorded value, change, share or historical series."));
                return;
            }
            if (intent.action != "execute") throw new ArgumentException("Unknown conversation action.");
            QuestionContract.Validate(question, session, intent);
            QueryValidator.Validate(intent);
            if (!string.IsNullOrWhiteSpace(intent.security)) {
                var candidates = await Task.Run(() => db.ResolveSecurity(account.Id, intent.security), token);
                if (!Active()) return;
                if (candidates.Count != 1) {
                    session.PendingQuestion ??= question;
                    Reply(candidates.Count == 0 ? $"I could not find '{intent.security}' in this account. Which recorded name or ticker do you mean?" :
                        "Which security do you mean?\n" + string.Join("\n", candidates.Take(15).Select(x => x.ToString())));
                    return;
                }
                intent.security = string.IsNullOrWhiteSpace(candidates[0].Symbol) ? candidates[0].Name : candidates[0].Symbol;
                var referenced = session.ResolveReference(question);
                if (referenced != null && !string.Equals(referenced, intent.security, StringComparison.OrdinalIgnoreCase)) {
                    session.PendingQuestion ??= question;
                    Reply($"I could not reliably match that reference. Did you mean {referenced}? Please use its name or ticker.");
                    return;
                }
            }
            var result = await Task.Run(() => new FinancialQueryService(db).Calculate(account, intent), token);
            token.ThrowIfCancellationRequested();
            if (!Active()) return;
            var context = OwnershipQueries.IsOwnership(intent.operation)
                ? $"{account.Name} · {FinancialQueryService.Describe(intent.operation)}"
                : $"{account.Name} · Requested {intent.start_date ?? "year start"} – {intent.end_date ?? "today"} · {FinancialQueryService.Describe(intent.operation)}";
            var response = context + "\n" + result.Text;
            if (!string.IsNullOrWhiteSpace(result.Note)) response += "\n" + result.Note;
            AddTurn(session, "AskCons", response);
            // An unavailable result is visible, but never replaces a valid financial context.
            if (result.Success) {
                var names = result.Data is List<RankingRow> rows ? rows.Select(x => x.SecurityKey).ToList() :
                    string.IsNullOrWhiteSpace(intent.security) ? new List<string>() : new List<string> {intent.security};
                session.Commit(intent, response, names);
            }
            preferredPresentation = intent.presentation ?? "auto";
            RenderResult(result.Data, result.Subtitle, result.Note, result.Text);
        }
        catch (OperationCanceledException) { if (generation == conversationGeneration) Reply("Request cancelled or timed out. You can retry."); }
        catch (Exception ex) { if (generation == conversationGeneration) Reply("Unable to answer: " + ex.Message); }
        finally {
            requestCancellation = null; isAsking = false; AskButton.IsEnabled = true;
            CancelButton.IsEnabled = false; AskButton.Content = "Ask →"; QuestionBox.Focus();
        }
    }
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
        var mode = DisplayMode == "Automatic" ? preferredPresentation switch { "text" => "Text", "table" => "Table", "graph" => "Graph", _ => "Automatic" } : DisplayMode;
        if (result is List<RankingRow> rows)
        {
            AnswerHeading.Text = "Holdings";
            if (mode == "Text") { AnswerScroll.Visibility = Visibility.Visible; return; }
            ConfigureRankingGrid(rows);
            ResultsGrid.Visibility = Visibility.Visible;
        }
        else if (result is List<HistoryRow> history) {
            AnswerHeading.Text = "Recorded holding history";
            if(mode=="Text") { AnswerScroll.Visibility=Visibility.Visible; return; }
            ResultsGrid.Columns.Clear();
            ResultsGrid.Columns.Add(Column("Period",nameof(HistoryRow.Period),null,1.5));
            ResultsGrid.Columns.Add(Column("Event",nameof(HistoryRow.Event),null,2));
            ResultsGrid.Columns.Add(Column("Quantity",nameof(HistoryRow.Quantity),null,1.5));
            ResultsGrid.Columns.Add(Column("Source",nameof(HistoryRow.Source),null,2));
            ResultsGrid.Columns[0].MinWidth=170; ResultsGrid.Columns[1].MinWidth=230;
            ResultsGrid.Columns[2].MinWidth=170; ResultsGrid.Columns[3].MinWidth=230;
            ResultsGrid.RowHeight=double.NaN; ResultsGrid.MinRowHeight=37;
            var historyStyle=new Style(typeof(TextBlock));
            historyStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
            historyStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4)));
            foreach(var column in ResultsGrid.Columns.OfType<DataGridTextColumn>()) column.ElementStyle=historyStyle;
            ResultsGrid.ItemsSource=history; ResultsGrid.Visibility=Visibility.Visible;
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
        ResultsGrid.RowHeight=37;
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
        ResultsGrid.RowHeight=37;
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
        double X(int index) => left + (chartPoints.Count == 1 ? pw / 2 : (chartPoints[index].Date - chartPoints[0].Date).TotalDays * pw / Math.Max(1, (chartPoints[^1].Date - chartPoints[0].Date).TotalDays));
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










