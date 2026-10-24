using System.Windows;using System.Windows.Controls;
namespace AskCons;
public partial class MainWindow:Window {
 private readonly ReadOnlyPortfolio db=new();private readonly LocalInterpreter interpreter=new();
 public MainWindow(){InitializeComponent();Loaded+=(_,_)=>Refresh();}
 private void Refresh(){try{var old=AccountBox.SelectedValue;var a=db.Accounts();AccountBox.ItemsSource=a;AccountBox.SelectedItem=a.FirstOrDefault(x=>Equals(x.Id,old))??a.FirstOrDefault();DbStatus.Text="Connected: read-only\n"+ReadOnlyPortfolio.DefaultPath;}catch(Exception e){DbStatus.Text="Not connected: "+e.Message;AnswerText.Text="Unable to read the Consolidation database. "+e.Message;}}
 private async void TestAiClick(object sender,RoutedEventArgs e)
 {
     AiStatus.Text = "Testing local Ollama...";
     try { AiStatus.Text = await interpreter.TestConnection(CancellationToken.None); }
     catch (Exception ex) { AiStatus.Text = "AI connection failed: " + ex.Message; }
 }
 private void RefreshClick(object sender,RoutedEventArgs e)=>Refresh();
 private void AccountChanged(object sender,SelectionChangedEventArgs e){if(AnswerText!=null && AccountBox.SelectedItem is Account a)AnswerText.Text=$"Selected account: {a.Name}. Enter a question below.";}
 private async void AskClick(object sender,RoutedEventArgs e){if(AccountBox.SelectedItem is not Account account){AnswerText.Text="Select an account first.";return;}var question=QuestionBox.Text.Trim();if(question.Length==0)return;AskButton.IsEnabled=false;AnswerText.Text="Interpreting your question locally...";try{var intent=await interpreter.Interpret(question,CancellationToken.None);AnswerText.Text=Answer(account,intent);}catch(Exception ex){AnswerText.Text="AI interpretation failed. Use Test AI connection to diagnose.\n\n"+ex.Message;}finally{AskButton.IsEnabled=true;}}
 private string Answer(Account a,QueryIntent i){var ops=new[]{"account_change","account_value","holding_change","holding_value"};if(!ops.Contains(i.operation))return "This question is not yet supported. No financial data was invented.";
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
