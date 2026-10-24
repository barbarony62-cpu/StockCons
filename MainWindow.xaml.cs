using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.IO;using System.Text.Json;using System.Collections.ObjectModel;
namespace AskCons;
public partial class MainWindow:Window {
 private readonly ReadOnlyPortfolio db=new();private readonly LocalInterpreter interpreter=new();
 private readonly ObservableCollection<string> questions=new();
 private bool loadingHistory;
 private static readonly string HistoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AskCons","questions.json");
 private Dictionary<string,List<string>> saved=new();
 private string AccountKey => (AccountBox.SelectedItem as Account)?.Id.ToString()??"";
 private void LoadHistory(){try{if(File.Exists(HistoryPath))saved=JsonSerializer.Deserialize<Dictionary<string,List<string>>>(File.ReadAllText(HistoryPath))??new();}catch{saved=new();}UpdateHistory();}
 private void UpdateHistory(){loadingHistory=true;questions.Clear();if(saved.TryGetValue(AccountKey,out var list))foreach(var q in list)questions.Add(q);HistoryList.ItemsSource=questions;loadingHistory=false;}
 private void Remember(string q){var key=AccountKey;if(string.IsNullOrEmpty(key))return;if(!saved.TryGetValue(key,out var list))saved[key]=list=new();list.RemoveAll(x=>string.Equals(x,q,StringComparison.OrdinalIgnoreCase));list.Insert(0,q);if(list.Count>100)list.RemoveRange(100,list.Count-100);try{Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);File.WriteAllText(HistoryPath,JsonSerializer.Serialize(saved));}catch{}UpdateHistory();}
 private void HistorySelected(object sender,SelectionChangedEventArgs e){if(loadingHistory||HistoryList.SelectedItem is not string q)return;HistoryList.SelectedItem=null;QuestionBox.Text=q;_ = AskAsync();}
 private void QuestionKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift)==0){e.Handled=true;_ = AskAsync();}}
 public MainWindow(){InitializeComponent();Loaded+=(_,_)=>{Refresh();LoadHistory();QuestionBox.Focus();};}
 private void Refresh(){try{var old=AccountBox.SelectedValue;var a=db.Accounts();AccountBox.ItemsSource=a;AccountBox.SelectedItem=a.FirstOrDefault(x=>Equals(x.Id,old))??a.FirstOrDefault();DbStatus.Text="Connected: read-only\n"+ReadOnlyPortfolio.DefaultPath;}catch(Exception e){DbStatus.Text="Not connected: "+e.Message;AnswerText.Text="Unable to read the Consolidation database. "+e.Message;}}
 private async void TestAiClick(object sender,RoutedEventArgs e)
 {
     AiStatus.Text = "Testing local Ollama...";
     try { AiStatus.Text = await interpreter.TestConnection(CancellationToken.None); }
     catch (Exception ex) { AiStatus.Text = "AI connection failed: " + ex.Message; }
 }
 private void RefreshClick(object sender,RoutedEventArgs e)=>Refresh();
 private void AccountChanged(object sender,SelectionChangedEventArgs e){if(AnswerText!=null && AccountBox.SelectedItem is Account a){AnswerText.Text=$"Selected account: {a.Name}. Enter a question below.";UpdateHistory();}}
 private async void AskClick(object sender,RoutedEventArgs e)=>await AskAsync();
 private async Task AskAsync(){if(!AskButton.IsEnabled)return;if(AccountBox.SelectedItem is not Account account){AnswerText.Text="Select an account first.";return;}var question=QuestionBox.Text.Trim();if(question.Length==0)return;Remember(question);AskButton.IsEnabled=false;AnswerText.Text="Interpreting your question locally...";try{if(LooksLikeRanking(question,out var worst)) { AnswerText.Text=AnswerRanking(account,new QueryIntent{operation=worst?"bottom_holding_changes":"top_holding_changes"},question); } else {var intent=await interpreter.Interpret(question,CancellationToken.None);AnswerText.Text=Answer(account,intent,question);}}catch(Exception ex){AnswerText.Text="AI interpretation failed. Use Test AI connection to diagnose.\n\n"+ex.Message;}finally{AskButton.IsEnabled=true;QuestionBox.Focus();}}
 private string AnswerRanking(Account account, QueryIntent intent, string question)
 {
     bool worst = intent.operation == "bottom_holding_changes";
     var start = ParseDate(intent.start_date) ?? new DateTime(DateTime.Today.Year,1,1);
     var end = ParseDate(intent.end_date) ?? DateTime.Today;
     if (end > DateTime.Today || start > end) return "Please provide a valid historical date range.";
     var result = db.RankHoldingChanges(account.Id,start,end);
     if (result is null) return "There are no saved snapshots on or before both requested dates. No values were estimated.";
     var sorted = worst ? result.Items.OrderBy(x=>x.Change).ToList() : result.Items.OrderByDescending(x=>x.Change).ToList();
     var count = 5;
     var match = System.Text.RegularExpressions.Regex.Match(question,@"\b(?:top|best|worst|bottom)\s+(\d{1,2})\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
     if(match.Success && int.TryParse(match.Groups[1].Value,out var n))count=Math.Clamp(n,1,25);
     var selected=sorted.Take(count).ToList();
     if(selected.Count==0)return "No holdings with recorded values were found for these snapshots.";
     var lines=selected.Select((x,index)=>$"{index+1}. {x.Name}{(string.IsNullOrWhiteSpace(x.Symbol)?"":" ("+x.Symbol+")")}  {x.Change:+#,##0.00;-#,##0.00;0.00} NIS\n    {x.StartValue:N2} → {x.EndValue:N2} NIS");
     return $"Account: {account.Name}\n{(worst?"Largest decreases":"Largest increases")} in holding value (NIS)\n{result.StartDate:yyyy-MM-dd} → {result.EndDate:yyyy-MM-dd}\n\n" + string.Join("\n\n",lines) + "\n\nThis is the difference between recorded holding values, regardless of purchases or sales; it is not investment profit. Holdings absent from a snapshot are counted as zero. Matching uses security symbols where available, otherwise names. Rankings include all recorded securities, including funds and bonds where present.";
 }
 private static bool LooksLikeRanking(string question,out bool worst)
 {
     var q=question.ToLowerInvariant();
     worst=q.Contains("worst")||q.Contains("bottom")||q.Contains("losers")||q.Contains("lowest")||q.Contains("גרוע")||q.Contains("הפסד")||q.Contains("יריד");
     return (worst||q.Contains("best")||q.Contains("top")||q.Contains("gainers")||q.Contains("biggest")||q.Contains("highest")||q.Contains("מוביל")||q.Contains("הכי טוב")||q.Contains("רווח")) &&
         (q.Contains("stock")||q.Contains("holding")||q.Contains("securit")||q.Contains("share")||q.Contains("מני")||q.Contains("נייר")||q.Contains("אחזק"));
 }
 private string Answer(Account a,QueryIntent i,string question){if(i.operation is "top_holding_changes" or "bottom_holding_changes")return AnswerRanking(a,i,question);
 if(LooksLikeRanking(question,out var worst))return AnswerRanking(a,new QueryIntent{operation=worst?"bottom_holding_changes":"top_holding_changes",start_date=i.start_date,end_date=i.end_date},question);
 var ops=new[]{"account_change","account_value","holding_change","holding_value"};if(!ops.Contains(i.operation))return question.Contains("best",StringComparison.OrdinalIgnoreCase)||question.Contains("top",StringComparison.OrdinalIgnoreCase)||question.Contains("stocks",StringComparison.OrdinalIgnoreCase)?"I cannot reliably rank your five biggest monetary gains from the currently available data. Please specify a period (for example, 2026 or since purchase). A true profit ranking also needs transaction-adjusted cost basis, including purchases and sales; holding-value changes alone are not profits.":"I cannot calculate this requested metric reliably with the operations currently implemented. No financial values have been invented.";
 var end=ParseDate(i.end_date)??DateTime.Today;if(end>DateTime.Today)return "Future valuation dates are not supported.";
 bool holding=i.operation.StartsWith("holding_");var security=i.security?.Trim();if(holding && (string.IsNullOrWhiteSpace(security)||security.Length>100))return "I need a security name to answer that question.";
 var current=holding?db.HoldingValue(a.Id,security!,end):db.AccountValue(a.Id,end);
 if(current is null)return "No matching historical valuation is available for this account and date. No values were estimated.";
 string label=holding?$"Holding: {security}":"Account value";
 string currentLine=$"Account: {a.Name}\n{label} on {current.Date:yyyy-MM-dd}: {current.Value:N2} NIS";
 if(i.operation.EndsWith("_value"))return currentLine+"\n\nSource: saved ConsolidationNG snapshot. Historical values are not live market quotes.";
 var start=ParseDate(i.start_date);if(start is null||start>end)return "The date range could not be interpreted safely. Please specify a start and end date.";
 var previous=holding?db.HoldingValue(a.Id,security!,start.Value):db.AccountValue(a.Id,start.Value);
 if(previous is null)return $"No starting valuation was found on or before {start:yyyy-MM-dd}. No return was estimated.";
 var diff=current.Value-previous.Value;var pct=Math.Abs(previous.Value)>0.00001? $"{diff/previous.Value*100:+0.00;-0.00;0.00}%":"Not defined (zero starting value)";
 var note=holding?"Holding-value changes may include purchases, sales and currency effects. They are NOT personal investment profit or market-price returns.":"Changes in account value include deposits, withdrawals and transfers. They are NOT cash-flow-adjusted investment returns.";
 return $"Account: {a.Name}\n{label}\nStart: {previous.Date:yyyy-MM-dd} — {previous.Value:N2} NIS\nEnd: {current.Date:yyyy-MM-dd} — {current.Value:N2} NIS\nChange: {diff:+#,##0.00;-#,##0.00;0.00} NIS ({pct})\n\n{note}\n\nData source: saved ConsolidationNG snapshots. Dates shown are actual stored valuation dates.";
 }
 private static DateTime? ParseDate(string? s)=>DateTime.TryParseExact(s,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d)?d:null;
}
