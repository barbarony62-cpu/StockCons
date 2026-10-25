using AskCons;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;
internal static class LargeHistoryChecks {
 public static void Run(Action<bool,string> check) {
  var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"askcons-large-"+Guid.NewGuid()+".db");
  try {
   using(var c=new SqliteConnection("Data Source="+path)) {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="""
   CREATE TABLE Accounts(Id INTEGER,FriendlyName TEXT);
   INSERT INTO Accounts VALUES(1,'Synthetic A'),(2,'Synthetic B');
   CREATE TABLE Snapshots(Id INTEGER,AccountId INTEGER,ValuationDate TEXT,CapturedAt TEXT,LiquidAssetsIls REAL);
   WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i+1 FROM n WHERE i<12000)
   INSERT INTO Snapshots SELECT i,CASE WHEN i<=6000 THEN 1 ELSE 2 END,date('1990-01-01',printf('+%d days',(i-1)/2)),printf('%05d',i%2),100000+i FROM n;
   CREATE TABLE Holdings(SnapshotId INTEGER,Name TEXT,Symbol TEXT,ValueIls REAL,Quantity REAL);
   WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i+1 FROM n WHERE i<8)
   INSERT INTO Holdings SELECT s.Id,CASE WHEN n.i=1 THEN 'Tesla' WHEN n.i=2 THEN 'Apple' ELSE 'Holding '||n.i END,CASE WHEN n.i=1 THEN 'TSLA' WHEN n.i=2 THEN 'AAPL' ELSE 'S'||n.i END,s.Id+n.i,10 FROM Snapshots s CROSS JOIN n;
   """;cmd.ExecuteNonQuery();}
   SqliteConnection.ClearAllPools();var before=SHA256.HashData(System.IO.File.ReadAllBytes(path));
   using(var sandbox=new AnalysisSandbox(path,1)) {
    check(Convert.ToInt64(sandbox.Query("SELECT COUNT(*) FROM AskLatestSnapshots").Rows[0][0])==3000,"Large history canonicalization keeps one latest capture per account/date");
    // For each duplicate date, odd captures have the lexically later CapturedAt (00001).
    check(Convert.ToInt64(sandbox.Query("SELECT Id FROM AskCurrentSnapshot").Rows[0][0])==5999,"Large history selects valuation date then newest capture, not maximum ID alone");
    var clock=Stopwatch.StartNew();var result=sandbox.Query("SELECT h.Symbol,h.ValueIls FROM AskCurrentHoldings h JOIN AskCurrentSnapshot s ON s.Id=h.SnapshotId WHERE h.Symbol IN ('TSLA','AAPL')");
    var count=sandbox.Query("SELECT s.AccountId,(SELECT COUNT(*) FROM AskCurrentHoldings h WHERE h.SnapshotId=s.Id) AS TotalPositions FROM AskCurrentSnapshot s");
    check(result.Rows.Count==2&&Convert.ToInt64(count.Rows[0][1])==8&&clock.Elapsed.TotalSeconds<5,"Comparison reads and whole-portfolio counts stay within budget over 12000 snapshots / 96000 holdings");
   }
   using(var timed=new AnalysisSandbox(path,1)) {
    bool translated=false;try{timed.Query("WITH RECURSIVE x(n) AS (SELECT 1 UNION ALL SELECT n+1 FROM x WHERE n<1000000000) SELECT SUM(n) FROM x");}catch(TimeoutException){translated=true;}
    check(translated&&Convert.ToInt64(timed.Query("SELECT 1").Rows[0][0])==1,"Execution-budget interruption becomes a clear timeout and does not poison later reads");
   }
   using(var all=new AnalysisSandbox(path,-1))check(all.Query("SELECT AccountId,Id FROM AskCurrentSnapshot ORDER BY AccountId").Rows.Count==2,"Large all-account history retains independent current snapshots");
   check(before.SequenceEqual(SHA256.HashData(System.IO.File.ReadAllBytes(path))),"Large-history performance fix leaves original database bytes unchanged");
  } finally {SqliteConnection.ClearAllPools();System.IO.File.Delete(path);}
 }
}

