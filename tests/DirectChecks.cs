using AskCons;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Security.Cryptography;
internal static class DirectChecks {
 public static void Run(Action<bool,string> check) {
  var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"askcons-direct-"+Guid.NewGuid()+".db");
  try {
   using(var db=new SqliteConnection("Data Source="+path)){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="""
   CREATE TABLE Accounts(Id INTEGER,FriendlyName TEXT);INSERT INTO Accounts VALUES(1,'Synthetic A'),(2,'Synthetic B');
   CREATE TABLE Snapshots(Id INTEGER,AccountId INTEGER,ValuationDate TEXT,CapturedAt TEXT,LiquidAssetsIls REAL);
   INSERT INTO Snapshots VALUES(1,1,'2024-12-31','1',1000),(2,1,'2025-01-31','1',1200),(3,1,'2025-01-31','2',1250),(4,1,'2025-02-28','1',1500),(5,1,'2025-04-30','1',1800),(6,2,'2025-01-31','1',90000),(7,1,'2025-05-01','1',NULL);
   CREATE TABLE Holdings(SnapshotId INTEGER,Name TEXT,Symbol TEXT,ValueIls REAL,SecurityType TEXT);
   INSERT INTO Holdings VALUES(1,'Apple','AAPL',100,'Stock'),(1,'Sigma 20/80','5136627',300,'Mutual Fund'),(5,'Apple','AAPL',200,'Stock'),(5,'Sigma 20/80','5136627',500,'Mutual Fund'),(5,'Unknown type','UNK',50,''),(7,'Apple','AAPL',210,'Stock'),(7,'Sigma 20/80','5136627',510,'Mutual Fund'),(7,'Unknown type','UNK',50,''),(6,'Other account holding','OTHER',90000,'Stock');
   CREATE TABLE SecurityHistoryEvents(AccountId INTEGER,Name TEXT,Symbol TEXT,FromDate TEXT,ToDate TEXT,Source TEXT,EventType TEXT);
   INSERT INTO SecurityHistoryEvents VALUES(1,'Apple','AAPL','2017-02-14','2017-02-14','Synthetic record','First owned');
   """;cmd.ExecuteNonQuery();}
   SqliteConnection.ClearAllPools();var hash=SHA256.HashData(System.IO.File.ReadAllBytes(path));
   var monthly=DirectPortfolio.TryCalculate("How much has my portfolio changed month by month in 2025?",new(1,"Synthetic A"),path,CancellationToken.None)!;
   var table=(DataTable)monthly.Answer.Data;
   check(table.Rows.Count==3&&table.Rows[0]["Closing NIS"].ToString()==1250m.ToString("N2")&&table.Rows[0]["Change NIS"].ToString()==250m.ToString("N2"),"Monthly changes calculate in C# from newest captures and prior year-end baseline without a model");
   check(table.Rows[1]["Change NIS"].ToString()==250m.ToString("N2")&&monthly.Answer.Text.Contains("Observation gap")&&!monthly.Answer.Text.Contains("Other account"),"Monthly results disclose missing-month gaps and stay in selected account");
   check(monthly.Details.Contains("ORIGINAL QUESTION")&&monthly.Details.Contains("SQL")&&monthly.Details.Contains("elapsed_ms")&&monthly.Details.Contains("CLASSIFICATION PROFILE"),"Deterministic diagnostics include original question, routing, SQL, classification values and timing");
   var value=DirectPortfolio.TryCalculate("What was my portfolio value on 2025-02-15?",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(value.Answer.Text.Contains("2025-01-31")&&value.Answer.Text.Contains(1250m.ToString("N2")),"Dated values use actual nearest prior observations and expose actual date");
   var namedDate=DirectPortfolio.TryCalculate("What was my portfolio value on February 15, 2025?",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(namedDate.Answer.Text.Contains("2025-01-31")&&namedDate.Answer.Text.Contains(1250m.ToString("N2")),"English month/day dates resolve to the same deterministic as-of value");
   var graph=DirectPortfolio.TryCalculate("Graph monthly portfolio changes in 2025",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(graph.Answer.Data is SeriesResult series&&series.Points.Count==3&&series.Points[0].Value==250,"Monthly chart requests reuse deterministic changes without AI arithmetic");
   var current=DirectPortfolio.TryCalculate("What is my current portfolio value?",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(current.Answer.Text.Contains("2025-04-30")&&current.Answer.Text.Contains(1800m.ToString("N2")),"Latest portfolio value skips a null newer account total without inventing zero");
   var stocks=DirectPortfolio.TryCalculate("Show the largest stocks by value",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(stocks.Answer.Text.Contains("AAPL")&&!stocks.Answer.Text.Contains("5136627\t")&&stocks.Details.Contains("Mutual Fund"),"Stocks-only ranking uses actual explicit SecurityType labels and profiles other types");
   var owned=DirectPortfolio.TryCalculate("When did I first buy Apple?",new(1,"Synthetic A"),path,CancellationToken.None)!;
   check(owned.Answer.Text.Contains("2017-02-14")&&owned.Answer.Text.Contains("not proof"),"First-owned dates bypass AI and retain source meaning");
   check(!ResearchPolicy.Relevant("How much has my portfolio changed month by month?")&&ResearchPolicy.Relevant("Which is better Apple or Tesla?"),"Web availability does not trigger research for local portfolio calculations");
   check(!PublicRelevance.Matches(new("5136627","סיגמא י 20/80",[]),new("Sigma-Aldrich chemicals","https://example.com","","Sigma government system"))&&PublicRelevance.Matches(new("5136627","סיגמא י 20/80",[]),new("קרן סיגמא 20/80 5136627","https://example.com","","נתוני קרן")),"Israeli fund research rejects unrelated Sigma organizations and accepts matching public ID");
   check(hash.SequenceEqual(SHA256.HashData(System.IO.File.ReadAllBytes(path))),"Direct calculations leave original database bytes unchanged");
  }finally{SqliteConnection.ClearAllPools();System.IO.File.Delete(path);}
 }
}
