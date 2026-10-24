using System.IO;
using AskCons;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

internal static class Program {
 static int checks;
 static void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
 static void Reject(Action action,string name) { try { action(); } catch(ArgumentException) { Check(true,name); return; } throw new Exception("Accepted: "+name); }
 [STAThread] static int Main() {
  System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.InvariantCulture;
  var path=Path.Combine(Path.GetTempPath(),"askcons-test-"+Guid.NewGuid()+".db");
  try {
   using(var c=new SqliteConnection("Data Source="+path+";Pooling=False")) {
    c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="""
    CREATE TABLE Accounts(Id INTEGER, FriendlyName TEXT);
    CREATE TABLE Snapshots(Id INTEGER, AccountId INTEGER, ValuationDate TEXT, CapturedAt TEXT, LiquidAssetsIls REAL);
    CREATE TABLE Holdings(SnapshotId INTEGER, Name TEXT, Symbol TEXT, ValueIls REAL);
    INSERT INTO Accounts VALUES(1,'Synthetic A'),(2,'Synthetic B');
    INSERT INTO Snapshots VALUES(1,1,'2025-01-01','01',1000),(2,1,'2025-09-30','01',1800),
    (3,2,'2025-09-30','01',90000),(4,1,'2025-09-30','02',2000),(5,1,'2025-10-01','01',NULL);
    INSERT INTO Holdings VALUES(1,'Tesla Inc','TSLA',100),(1,'Tesla Bond','TB',200),
    (2,'Tesla Inc','TSLA',200),(4,'Tesla Inc','TSLA',300),(4,'Tesla Bond','TB',100),
    (3,'Private B','PB',90000),(5,'Tesla Inc','TSLA',350),
    (1,'Zero Position','ZERO',0),(4,'Zero Position','ZERO',0),(1,'Closed Position','CLOSED',50);
    """;
    cmd.ExecuteNonQuery();
   }
   var hash=SHA256.HashData(File.ReadAllBytes(path));
   var db=new ReadOnlyPortfolio(path); var end=new DateTime(2025,9,30);
   Check(db.ResolveSecurity(1,"Tesla").Count==2,"Ambiguous names require a choice");
   Check(db.ResolveSecurity(1,"TSLA").Single().Name=="Tesla Inc","Exact ticker resolves");
   Check(db.ResolveSecurity(1,"PB").Count==0,"Security resolution is scoped to account");
   Check(db.HoldingValue(1,"TSLA",end)!.Value==300,"Latest same-day capture wins");
   Check(db.HoldingValue(1,"Tesla",end)==null,"Partial name cannot silently sum instruments");
   Check(db.HistoricalSeries(1,"unknown",new(2025,1,1),end,"month")==null,"Unknown security never produces zero chart");
   var series=db.HistoricalSeries(1,"TSLA",new(2025,1,1),end,"month")!;
   Check(series.Points.Count==2 && series.Points.Last().Value==300,"Historical series deduplicates captures");
   var share=db.HoldingShare(1,"TSLA",end)!.Value;
   Check(share.Holding.Value/share.Account.Value==0.15,"Share uses one snapshot");
   Check(db.HoldingShare(1,"TSLA",new(2025,10,1))==null,"Missing latest account total is not mixed with older total");
   var service=new FinancialQueryService(db);
   QueryIntent Rank(string sort,string dir)=>new(){operation="holdings_list",start_date="2025-01-01",end_date="2025-09-30",sort_by=sort,sort_direction=dir,name_contains="Tesla"};
   var ascending=(List<RankingRow>)service.Calculate(new(1,"A"),Rank("name","asc")).Data;
   Check(ascending[0].Security=="Tesla Bond","Name ascending works");
   var descending=(List<RankingRow>)service.Calculate(new(1,"A"),Rank("name","desc")).Data;
   Check(descending[0].Security=="Tesla Inc","Name descending works");
   var byChange=(List<RankingRow>)service.Calculate(new(1,"A"),Rank("change","asc")).Data;
   Check(byChange[0].Change==-100 && byChange[1].Change==200,"Change ascending works");
   Reject(()=>QueryValidator.Validate(new(){operation="account_value",name_contains="Tesla"}),"Reject ignored filter");
   Reject(()=>QueryValidator.Validate(new(){operation="holding_value",security="TSLA",security_type="stock"}),"Reject unavailable classification");
   Reject(()=>QueryValidator.Validate(new(){operation="account_change",start_date="2025-10-01",end_date="2025-01-01"}),"Reject reversed dates");
   Reject(()=>QueryValidator.Validate(new(){operation="holdings_filter",minimum_change_nis=200,maximum_change_nis=100}),"Reject contradictory ranges");
   var disappeared = (List<RankingRow>)service.Calculate(new(1,"A"),new QueryIntent {operation="holdings_disappeared",start_date="2025-01-01",end_date="2025-09-30"}).Data;
   Check(disappeared.Count==1 && disappeared[0].SecurityKey=="CLOSED","Zero-valued holdings are not mistaken for absent holdings");
   var accountChange=service.Calculate(new(1,"A"),new QueryIntent{operation="account_change",start_date="2025-01-01",end_date="2025-09-30"});
   Check(accountChange.Success && accountChange.Text.Contains("+100.00%"),"Account value doubles: percentage change is 100 percent");
   Reject(()=>QueryValidator.Validate(new(){operation="holdings_filter",absent_at_end=true,require_present_at_end=true}),"Reject contradictory presence restrictions");
   var session=new ConversationSession(); var plan=Rank("change","desc");
   session.Commit(plan,"Verified result",new[]{"TSLA","TB"}); plan.end_date="2020-01-01";
   Check(session.LastPlan!.end_date=="2025-09-30","Committed context is immutable from caller");
   Check(session.ResolveReference("Graph the second one")=="TB" && session.ResolveReference("Graph the fifth holding")==null,"Ordinal resolution uses the actual ordered result");
   session.PendingQuestion="Which Tesla?";
   session.Turns.Add(("You","Graph the second one"));
   var handler=new FakeHandler();
   var interpreter=new LocalInterpreter(new HttpClient(handler){BaseAddress=new("http://127.0.0.1:11434/")});
   handler.Answer="""{"action":"execute","operation":"holding_series","security":"TB","start_date":"2025-01-01","end_date":"2025-09-30","interval":"month"}""";
   var follow=interpreter.Interpret("Graph the second one",session,CancellationToken.None).GetAwaiter().GetResult();
   Check(follow.security=="TB","Structured follow-up response is parsed");
   using(var request=JsonDocument.Parse(handler.Request)) {
    var messages=request.RootElement.GetProperty("messages");
    Check(messages[0].GetProperty("role").GetString()=="system" && messages[1].GetProperty("role").GetString()=="user","User input is separate from system instructions");
    var context=messages[0].GetProperty("content").GetString()!;
    Check(context.Contains("ordered_securities") && context.Contains("TSLA") && context.Contains("Which Tesla?"),"Follow-up includes plan, ordered identities and pending clarification");
   }
   handler.Answer="""{"action":"clarify","operation":"unsupported","clarification":"Value change or profit?"}""";
   Check(interpreter.Interpret("How much did I make?",session,CancellationToken.None).GetAwaiter().GetResult().action=="clarify","Clarification requires no executable plan");
   using(var cancelled=new CancellationTokenSource()) {
    cancelled.Cancel();
    try { interpreter.Interpret("Cancel",session,cancelled.Token).GetAwaiter().GetResult(); throw new Exception("Cancellation ignored"); }
    catch(OperationCanceledException) { Check(true,"Cancellation propagates"); }
   }
   Reject(()=>new LocalInterpreter(new HttpClient(){BaseAddress=new("https://example.com/")}),"Reject remote AI endpoint");
   Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),"All financial queries leave database bytes unchanged");
   // Instantiate and lay out the compiled WPF controls without loading a real portfolio.
   var window=new MainWindow();
   var root=(System.Windows.FrameworkElement)window.Content;
   root.Measure(new System.Windows.Size(1280,900)); root.Arrange(new System.Windows.Rect(0,0,1280,900)); root.UpdateLayout();
   Check(window.FindName("Transcript") != null && window.FindName("CancelButton") != null,"Compiled conversation UI constructs and lays out");
   if (Environment.GetEnvironmentVariable("ASKCONS_PREVIEW") is string preview) {
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1280,900,96,96,System.Windows.Media.PixelFormats.Pbgra32);
    bitmap.Render(root); var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using var output=File.Create(preview); encoder.Save(output);
   }
   window.Close();
   if(Environment.GetEnvironmentVariable("ASKCONS_LIVE_MODEL")=="1") {
    var live=new LocalInterpreter(); var conversation=new ConversationSession{SelectedAccount="Synthetic A"};
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
    var initial=live.Interpret("Show the two holdings with the largest recorded NIS value increase from January 1 to September 30, 2025.",conversation,timeout.Token).GetAwaiter().GetResult();
    Console.WriteLine("LIVE initial: "+JsonSerializer.Serialize(initial));
    Check(initial.action=="execute" && initial.limit==2 && initial.operation=="top_holding_changes" && initial.start_date=="2025-01-01" && initial.end_date=="2025-09-30","Live model interprets explicit value-change ranking");
    QueryValidator.Validate(initial);
    conversation.Commit(initial,"1. Tesla Inc; 2. Tesla Bond",new[]{"TSLA","TB"});
    conversation.Turns.Add(("You","Show the two holdings with the largest recorded NIS value increase from January 1 to September 30, 2025."));
    conversation.Turns.Add(("AskCons","1. Tesla Inc; 2. Tesla Bond"));
    var next=live.Interpret("Graph the second one monthly.",conversation,timeout.Token).GetAwaiter().GetResult();
    Console.WriteLine("LIVE follow-up: "+JsonSerializer.Serialize(next));
    Check(next.action=="execute" && next.presentation=="graph" && next.operation=="holding_series" && next.security=="TB" && next.start_date=="2025-01-01" && next.end_date=="2025-09-30","Live model retains period and resolves second holding");
    QueryValidator.Validate(next);
    var ambiguity=live.Interpret("How much profit did I make on Tesla?",new ConversationSession{SelectedAccount="Synthetic A"},timeout.Token).GetAwaiter().GetResult();
    Console.WriteLine("LIVE ambiguity: "+JsonSerializer.Serialize(ambiguity));
    Check(ambiguity.action is "clarify" or "unsupported" || ambiguity.operation=="unsupported","Live model does not invent transaction-adjusted profit");
   }
   Console.WriteLine(checks+" checks passed."); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally { SqliteConnection.ClearAllPools(); if(File.Exists(path)) File.Delete(path); }
 }
 sealed class FakeHandler:HttpMessageHandler {
  public string Answer=""; public string Request="";
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
   token.ThrowIfCancellationRequested(); Request=await request.Content!.ReadAsStringAsync(token);
   return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{message=new{content=Answer}}),Encoding.UTF8,"application/json")};
  }
 }
}









