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
    CREATE TABLE ReferenceFx(Date TEXT, Rate REAL);
    INSERT INTO ReferenceFx VALUES('2025-09-30',4.0);
    CREATE TABLE Accounts(Id INTEGER, FriendlyName TEXT);
    CREATE TABLE Snapshots(Id INTEGER, AccountId INTEGER, ValuationDate TEXT, CapturedAt TEXT, LiquidAssetsIls REAL);
    CREATE TABLE Holdings(SnapshotId INTEGER, Name TEXT, Symbol TEXT, ValueIls REAL, Quantity REAL);
    CREATE TABLE SecurityHistoryEvents(Id INTEGER PRIMARY KEY,AccountId INTEGER,FromDate TEXT,ToDate TEXT,Name TEXT,Symbol TEXT,FromQuantity REAL,ToQuantity REAL,EventType TEXT,Source TEXT);
    INSERT INTO Accounts VALUES(1,'Synthetic A'),(2,'Synthetic B'),(3,'Baseline test');
    INSERT INTO Snapshots VALUES(1,1,'2025-01-01','01',1000),(2,1,'2025-09-30','01',1800),
    (3,2,'2025-09-30','01',90000),(4,1,'2025-09-30','02',2000),(5,1,'2025-10-01','01',NULL);
    INSERT INTO Holdings(SnapshotId,Name,Symbol,ValueIls) VALUES(1,'Tesla Inc','TSLA',100),(1,'Tesla Bond','TB',200),
    (2,'Tesla Inc','TSLA',200),(4,'Tesla Inc','TSLA',300),(4,'Tesla Bond','TB',100),
    (3,'Private B','PB',90000),(5,'Tesla Inc','TSLA',350),
    (1,'Zero Position','ZERO',0),(4,'Zero Position','ZERO',0),(1,'Closed Position','CLOSED',50),
    (1,'סה"כ','',1000000),(4,'סה״כ:','',3000000),
    (1,'TotalEnergies SE','TTE',50),(4,'TotalEnergies SE','TTE',75);
    INSERT INTO Snapshots VALUES(6,3,'2024-12-31','01',100),(7,3,'2025-01-03','01',110),(8,3,'2025-09-30','01',130);
    INSERT INTO Holdings(SnapshotId,Name,Symbol,ValueIls) VALUES(6,'Old Only','OLD',10),(7,'Opening Holding','OPEN',100),(8,'Opening Holding','OPEN',120);
    """;
    cmd.ExecuteNonQuery();
    cmd.CommandText="""
    UPDATE Holdings SET Quantity=10 WHERE Symbol='TSLA' AND SnapshotId=1;
    UPDATE Holdings SET Quantity=15 WHERE Symbol='TSLA' AND SnapshotId=2;
    UPDATE Holdings SET Quantity=30 WHERE Symbol='TSLA' AND SnapshotId=4;
    INSERT INTO SecurityHistoryEvents VALUES
      (1,1,'2017-02-14','2017-02-14','Tesla Inc','TSLA',NULL,NULL,'First owned','Synthetic first-owned record'),
      (2,1,'2019-05-06','2019-05-06','Tesla Inc','TSLA',NULL,20,'Historical quantity observed','Synthetic historical observation'),
      (3,1,'2021-07-02','2021-07-09','Tesla Inc','TSLA',20,40,'Corporate action / quantity adjustment','Synthetic adjustment record'),
      (4,2,'1999-01-01','1999-01-01','Tesla Inc','TSLA',NULL,NULL,'First owned','Other account'),
      (5,1,'2018-03-04','2018-03-04','History Only','HIST',NULL,NULL,'First owned','Synthetic archived position'),
      (6,1,'2020-01-01','2020-01-01','Tesla Bond','TB',NULL,5,'Historical quantity observed','Synthetic observation'),
      (7,1,'2019-06-01','2019-06-01','FIRST TRUST NASDAQ ETF','QQEW',NULL,NULL,'First owned','Synthetic separate holding');
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
   Check(QuestionPolicy.NeedsMetricClarification("Give me the best 5 holdings from a NIS perspective in 2026, provided that the stocks existed on the first recorded day in 2026."),"Screenshot question requires metric clarification");
   Check(!QuestionPolicy.NeedsMetricClarification("Show Best Buy") && !HoldingIdentity.IsSummaryLabel("TotalEnergies SE") && !HoldingIdentity.IsSummaryLabel("Total","FP"),"Real company names are not classified by substring");
   Check(HoldingIdentity.IsSummaryLabel("סה״כ:") && HoldingIdentity.IsSummaryLabel("סה\"כ") && HoldingIdentity.IsSummaryLabel("Grand total"),"Summary punctuation and Hebrew quote variants recognized");
   Check(db.ResolveSecurity(1,"סה").Count==0 && db.HoldingValue(1,"סה\"כ",end)==null && db.HoldingShare(1,"סה\"כ",end)==null,"Totals excluded from resolution, valuation and share");
   Check(db.HistoricalSeries(1,"סה\"כ",new(2025,1,1),end,"month")==null,"Totals excluded from security history");
   var top=(List<RankingRow>)service.Calculate(new(1,"A"),new QueryIntent{operation="top_holding_changes",start_date="2025-01-01",end_date="2025-09-30",limit=5}).Data;
   Check(top.All(x=>!HoldingIdentity.IsSummaryLabel(x.Security)) && top.First().SecurityKey=="TSLA" && top.Zip(top.Skip(1)).All(pair=>pair.First.Change>=pair.Second.Change),"Only real holdings ranked in descending value-change order");
   Check(db.AccountValue(1,end)!.Value==2000,"Summary exclusion does not remove the account total");
   Reject(()=>QueryValidator.Validate(new(){operation="top_holding_changes",sort_by="start_value"}),"Reject top-change plan sorted by position size");
   var opening=service.Calculate(new(3,"Baseline"),new QueryIntent{operation="top_holding_changes",start_date="2025-01-01",end_date="2025-09-30",first_recorded_start=true,present_at_start=true});
   Check(opening.Success && ((List<RankingRow>)opening.Data).Single().SecurityKey=="OPEN" && opening.Subtitle.Contains("03 Jan 2025"),"First recorded in-period snapshot defines opening membership");
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
   var owned=service.Calculate(new(1,"A"),new QueryIntent{operation="holding_first_owned",security="TSLA"});
   Check(owned.Success && owned.Text.Contains("14 Feb 2017") && owned.Text.Contains("Recorded first owned") && owned.Text.Contains("not proof"),"Explicit first-owned event answers date without asserting purchase");
   Check(owned.Text.Contains("Synthetic first-owned record") && !owned.Text.Contains("1999"),"First-owned source and account scope preserved");
   Check(db.ResolveSecurity(1,"HIST").Single().Name=="History Only","Archived security resolves from stored events without snapshots");
   Check(service.Calculate(new(1,"A"),new QueryIntent{operation="holding_first_owned",security="HIST"}).Text.Contains("04 Mar 2018"),"History-only holding can answer first-owned date");
   var noOwned=service.Calculate(new(1,"A"),new QueryIntent{operation="holding_first_owned",security="TB"});
   Check(!noOwned.Success && noOwned.Text.Contains("no explicit") && !noOwned.Text.Contains("2020-01-01"),"Quantity observation is never used as first purchase date");
   var fullHistory=service.Calculate(new(1,"A"),new QueryIntent{operation="holding_history",security="TSLA"});
   var historyRows=(List<HistoryRow>)fullHistory.Data;
   Check(historyRows.Any(x=>x.Event=="First owned") && historyRows.Any(x=>x.Event=="Corporate action / quantity adjustment") && historyRows.Any(x=>x.Source=="Consolidation portfolio snapshot"),"History includes recorded events and explicitly labelled quantity observations");
   Check(!historyRows.Any(x=>x.Source=="Other account"),"Event history never crosses accounts");
   var observations=db.ObservedQuantities(1,new SecurityCandidate("Tesla Inc","TSLA"));
   Check(observations.Count==2 && observations.Last().Quantity==30,"Quantity history uses newest same-day capture and skips nulls");
   var bounded=service.Calculate(new(1,"A"),new QueryIntent{operation="holding_history",security="TSLA",start_date="2021-01-01",end_date="2021-12-31"});
   Check(((List<HistoryRow>)bounded.Data).Count==1 && ((List<HistoryRow>)bounded.Data)[0].Event=="Corporate action / quantity adjustment","Explicit history range filters events without year-to-date default");
   var cleanOwned=QueryValidator.Validate(new(){operation="holding_first_owned",security="TSLA"});
   Check(cleanOwned.start_date==null && cleanOwned.end_date==null,"First-owned query has no year-to-date window");
   Reject(()=>QueryValidator.Validate(new(){operation="holding_first_owned",security="TSLA",start_date="2025-01-01"}),"Reject date-window contamination on first-owned query");
   Reject(()=>QueryValidator.Validate(new(){operation="holding_history",security="TSLA",sort_by="start_value"}),"Reject ranking filters on history query");
   var stale=new ConversationSession{SelectedAccount="A"};
   stale.Commit(new QueryIntent{operation="holdings_disappeared",start_date="2025-01-01",end_date="2025-09-30",sort_by="start_value"},"Old ranking",new[]{"TB"});
   var question="when did I first buy tesla?";
   Check(QuestionContract.RequiredOperation(question,stale)=="holding_first_owned","Screenshot question selects ownership answer family");
   var freshContext=JsonSerializer.Serialize(QuestionContract.PlannerContext(question,stale));
   Check(!freshContext.Contains("start_value") && !freshContext.Contains("holdings_disappeared") && !freshContext.Contains("2025-01-01"),"New ownership question excludes stale ranking and dates from context");
   Reject(()=>QuestionContract.Validate(question,stale,new(){operation="holdings_disappeared"}),"Reject exact unrelated-answer failure before database execution");
   handler.Answer="""{"action":"execute","operation":"holdings_disappeared","start_date":"2025-01-01","end_date":"2025-09-30"}""";
   Reject(()=>interpreter.Interpret(question,stale,CancellationToken.None).GetAwaiter().GetResult(),"Interpreter independently rejects unrelated valid JSON response");
   handler.Answer="""{"action":"execute","operation":"holding_first_owned","security":"TSLA"}""";
   var interpreted=interpreter.Interpret(question,stale,CancellationToken.None).GetAwaiter().GetResult();
   Check(service.Calculate(new(1,"A"),interpreted).Text.Contains("14 Feb 2017"),"Ranking to first-owned conversation reaches stored date");
   using(var req=JsonDocument.Parse(handler.Request)) {
       var format=req.RootElement.GetProperty("format").GetRawText();
       Check(format.Contains("holding_first_owned") && !format.Contains("holdings_disappeared"),"Question-specific schema cannot generate an unrelated ranking");
   }
   stale.Commit(cleanOwned,owned.Text,new[]{"TSLA"});
   Check(QuestionContract.RequiredOperation("And Apple?",stale)=="holding_first_owned","Short follow-up retains ownership question family");
   // A pre-history database is handled without schema changes or fabricated dates.
   var pendingOwnership=new ConversationSession{PendingQuestion="when did I first buy tesla?"};
   Check(QuestionContract.RequiredOperation("TSLA",pendingOwnership)=="holding_first_owned","Security clarification reply retains pending ownership intent");
   Check(!JsonSerializer.Serialize(QuestionContract.PlannerContext("when did I first buy tesla?",new ConversationSession{PendingQuestion="best 5 stocks"})).Contains("best 5 stocks"),"New ownership question does not inherit an unrelated pending clarification");
   Check(historyRows.Zip(historyRows.Skip(1)).All(p=>p.First.EventDate<=p.Second.EventDate),"Combined history table is chronological");
   Check(!QuestionContract.AllowsHistoryDates("Show holding history for TSLA.",stale) && QuestionContract.AllowsHistoryDates("Show holding history since 2022 for TSLA.",stale),"History dates require an explicit period");
   Reject(()=>QuestionContract.Validate("Show holding history for TSLA.",stale,new(){operation="holding_history",security="TSLA",start_date="2025-01-01"}),"Reject invented year-to-date history restriction");
   Check(!QuestionContract.AllowsHistoryDates("Show holding history for TSLA.",new ConversationSession{PendingQuestion="best stocks in 2026"}),"Unrelated pending ranking cannot supply history dates");
   Reject(()=>QuestionContract.Validate("when did I first buy tesla?",stale,new(){action="explain"}),"Ownership question cannot repeat an unrelated previous answer");
   var oldPath=path+".old";
   using(var c=new SqliteConnection("Data Source="+oldPath+";Pooling=False")) {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE Placeholder(Id INTEGER)";cmd.ExecuteNonQuery();}
   try { new ReadOnlyPortfolio(oldPath).StoredHistory(1,new("Tesla Inc","TSLA"));throw new Exception("Missing table accepted");}
   catch(InvalidOperationException ex){Check(ex.Message.Contains("no SecurityHistoryEvents"),"Old database reports missing history capability clearly");}
   finally {File.Delete(oldPath);}
   AnalysisChecks.Run(path,Check);
   Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),"All financial queries leave database bytes unchanged");
   // Instantiate and lay out the compiled WPF controls without loading a real portfolio.
   var window=new MainWindow();
   Check(((System.Windows.Controls.CheckBox)window.FindName("AnalystModeBox")).IsChecked==true,"Open-ended analyst is the default chat mode");
   var root=(System.Windows.FrameworkElement)window.Content;
   root.Measure(new System.Windows.Size(1280,900)); root.Arrange(new System.Windows.Rect(0,0,1280,900)); root.UpdateLayout();
   Check(window.FindName("Transcript") != null && window.FindName("CancelButton") != null,"Compiled conversation UI constructs and lays out");
   typeof(MainWindow).GetMethod("RenderResult",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,new object[]{historyRows,fullHistory.Subtitle,fullHistory.Note,fullHistory.Text});
   root.Measure(new System.Windows.Size(1280,900)); root.Arrange(new System.Windows.Rect(0,0,1280,900)); root.UpdateLayout();
   Check(((System.Windows.Controls.DataGrid)window.FindName("ResultsGrid")).Items.Count==historyRows.Count,"Compiled WPF history table renders event and quantity rows");
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
   if(Environment.GetEnvironmentVariable("ASKCONS_LIVE_OWNERSHIP")=="1") {
     var live=new LocalInterpreter(); var conversation=new ConversationSession{SelectedAccount="Synthetic A"};
     conversation.Commit(new QueryIntent{operation="holdings_disappeared",start_date="2025-01-01",end_date="2025-09-30",sort_by="start_value"},"Prior ranking",new[]{"TB"});
     using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
     var planOwned=live.Interpret("when did I first buy tesla?",conversation,timeout.Token).GetAwaiter().GetResult();
     Console.WriteLine("LIVE ownership: "+JsonSerializer.Serialize(planOwned));
     Check(planOwned.action=="execute" && planOwned.operation=="holding_first_owned" && !string.IsNullOrWhiteSpace(planOwned.security) && planOwned.start_date==null && planOwned.end_date==null && planOwned.sort_by==null,"Live model changes ranking to all-time first-owned query");
     QueryValidator.Validate(planOwned);
     conversation.Commit(new QueryIntent{operation="holding_first_owned",security="TSLA"},"Synthetic recorded first owned",new[]{"TSLA"});
     var planHistory=live.Interpret("Show the holding history for TSLA.",conversation,timeout.Token).GetAwaiter().GetResult();
     Console.WriteLine("LIVE history: "+JsonSerializer.Serialize(planHistory));
     Check(planHistory.action=="execute" && planHistory.operation=="holding_history" && planHistory.security=="TSLA" && planHistory.start_date==null && planHistory.end_date==null,"Live history request clears dates and preserves named security");
     QueryValidator.Validate(planHistory);
   }
   if(Environment.GetEnvironmentVariable("ASKCONS_LIVE_ANALYST")=="1")AnalysisChecks.Live(path,Check);
   if(Environment.GetEnvironmentVariable("ASKCONS_LIVE_OWNER_ONLY")=="1")AnalysisChecks.LiveOwner(path,Check);
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














