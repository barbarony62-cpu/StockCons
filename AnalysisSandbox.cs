using System.IO;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SQLitePCL;
namespace AskCons;

// Model SQL NEVER runs against the source file. Only account-scoped rows are copied into RAM.
public sealed class AnalysisSandbox : IDisposable
{
    private readonly SqliteConnection memory = new("Data Source=:memory:;Pooling=False");
    private readonly List<object> tables=new();
    private readonly List<object> securities=new();
    private readonly Dictionary<long,string> accountNames=new();
    private readonly List<object> ownershipMarkers=new();
    private readonly List<string> eventTypes=new();
    public List<string> Warnings {get;}=new();
    public long AccountId {get;}
    public AnalysisSandbox(string path,long accountId,CancellationToken token=default) {
        if(accountId<0 && accountId!=-1)throw new ArgumentException("Invalid account scope.");
        AccountId=accountId;
        try {
        if(!File.Exists(path)) throw new FileNotFoundException("Portfolio database not found.",path);
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
        source.Open(); using(var q=source.CreateCommand()){q.CommandText="PRAGMA query_only=ON";q.ExecuteNonQuery();}
        using var readTransaction=source.BeginTransaction(deferred:true);
        memory.Open();
        using(var temp=memory.CreateCommand()){temp.CommandText="PRAGMA temp_store=MEMORY";temp.ExecuteNonQuery();}
        var definitions=new List<(string Name,List<(string Name,string Type)> Columns)>();
        using(var cmd=source.CreateCommand()) {
            cmd.Transaction=readTransaction;
            cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var r=cmd.ExecuteReader(); var names=new List<string>();
            while(r.Read()) names.Add(r.GetString(0));
            r.Close();
            foreach(var name in names) {
                using var info=source.CreateCommand(); info.Transaction=readTransaction; info.CommandText="PRAGMA table_info("+Quote(name)+")";
                using var ir=info.ExecuteReader();var cols=new List<(string,string)>();
                while(ir.Read()) cols.Add((ir.GetString(1),ir.GetString(2)));
                definitions.Add((name,cols));
            }
        }
        int cells=0; long copiedBytes=0; const int cellBudget=3_000_000; const long byteBudget=128L*1024*1024;
        using(var transaction=memory.BeginTransaction()) { foreach(var definition in definitions.OrderBy(d=>d.Name=="Accounts"?0:d.Name=="Snapshots"?1:d.Name=="Holdings"?3:2)) {
            token.ThrowIfCancellationRequested();
            var names=definition.Columns.Select(x=>x.Name).ToList();
            string? condition=names.Any(x=>x.Equals("AccountId",StringComparison.OrdinalIgnoreCase))?"AccountId=$account":
                definition.Name.Equals("Accounts",StringComparison.OrdinalIgnoreCase) && names.Contains("Id")?"Id=$account":
                names.Any(x=>x.Equals("SnapshotId",StringComparison.OrdinalIgnoreCase)) && definitions.Any(x=>x.Name=="Snapshots")?
                "SnapshotId IN (SELECT Id FROM Snapshots WHERE AccountId=$account)":null;
            if(accountId==-1)condition="1=1";
            if(condition==null) { Warnings.Add("Not copied: "+definition.Name+" (no verified account relationship)."); continue; }
            using(var create=memory.CreateCommand()) {
                create.Transaction=transaction;
                create.CommandText="CREATE TABLE "+Quote(definition.Name)+" ("+string.Join(",",definition.Columns.Select(x=>Quote(x.Name)+" "+Affinity(x.Type)))+")";
                create.ExecuteNonQuery();
            }
            using var select=source.CreateCommand(); select.Transaction=readTransaction; select.CommandText="SELECT * FROM "+Quote(definition.Name)+" WHERE "+condition;
            if(condition.Contains("$account"))select.Parameters.AddWithValue("$account",accountId);
            using var reader=select.ExecuteReader();
            using var insert=memory.CreateCommand();insert.Transaction=transaction;
            insert.CommandText="INSERT INTO "+Quote(definition.Name)+" VALUES ("+string.Join(",",names.Select((_,i)=>"$p"+i))+")";
            for(int i=0;i<names.Count;i++)insert.Parameters.Add(new SqliteParameter("$p"+i,DBNull.Value));
            int rows=0;bool truncated=false;
            while(reader.Read()) {
                token.ThrowIfCancellationRequested();
                if(cells+names.Count>cellBudget) { truncated=true;break; }
                for(int i=0;i<names.Count;i++)insert.Parameters[i].Value=reader.GetValue(i);
                long rowBytes=insert.Parameters.Cast<SqliteParameter>().Sum(p=>p.Value is string text?2L*text.Length:p.Value is byte[] bytes?bytes.LongLength:16L);
                if(copiedBytes+rowBytes>byteBudget){truncated=true;break;}
                insert.ExecuteNonQuery();rows++;cells+=names.Count;copiedBytes+=rowBytes;
            }
            if(truncated) Warnings.Add("PARTIAL DATA: "+definition.Name+" exceeded the in-memory copy budget. Calculations using it may be incomplete.");
            tables.Add(new {table=definition.Name,columns=definition.Columns.Select(x=>new{name=x.Name,type=x.Type}),copied_rows=rows,complete=!truncated});
        }
        transaction.Commit(); }
        readTransaction.Commit();
        if(definitions.Any(d=>d.Name=="Accounts" && d.Columns.Any(c=>c.Name=="FriendlyName"))) {
            using var names=memory.CreateCommand();names.CommandText="SELECT Id,FriendlyName FROM Accounts";
            using var ar=names.ExecuteReader();while(ar.Read())accountNames[ar.GetInt64(0)]=ar.GetString(1);
        }
        double? Finite(double value)=>double.IsFinite(value)?value:null;
        memory.CreateFunction<double?,double?,double?>("pow",(x,y)=>x.HasValue&&y.HasValue?Finite(Math.Pow(x.Value,y.Value)):null,isDeterministic:true);
        memory.CreateFunction<double?,double?,double?>("power",(x,y)=>x.HasValue&&y.HasValue?Finite(Math.Pow(x.Value,y.Value)):null,isDeterministic:true);
        memory.CreateFunction<double?,double?>("sqrt",x=>x.HasValue?Finite(Math.Sqrt(x.Value)):null,isDeterministic:true);
        memory.CreateFunction<double?,double?>("ln",x=>x.HasValue?Finite(Math.Log(x.Value)):null,isDeterministic:true);
        memory.CreateFunction<double?,double?>("log",x=>x.HasValue?Finite(Math.Log10(x.Value)):null,isDeterministic:true);
        memory.CreateFunction<double?,double?>("exp",x=>x.HasValue?Finite(Math.Exp(x.Value)):null,isDeterministic:true);
        memory.CreateFunction<string,string,bool>("is_summary",HoldingIdentity.IsSummaryLabel,isDeterministic:true);
        // Source indexes are not part of SELECT * copies. Rebuild only RAM indexes before enabling query_only.
        foreach(var definition in definitions) {
            using var exists=memory.CreateCommand();exists.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";exists.Parameters.AddWithValue("$n",definition.Name);
            if(Convert.ToInt64(exists.ExecuteScalar())==0)continue;
            var available=definition.Columns.Select(c=>c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var indexSets=new List<string[]>();
            if(available.Contains("Id"))indexSets.Add(new[]{"Id"});
            if(available.Contains("SnapshotId"))indexSets.Add(new[]{"SnapshotId"});
            if(available.Contains("AccountId")&&available.Contains("ValuationDate"))indexSets.Add(new[]{"AccountId","ValuationDate","CapturedAt","Id"}.Where(available.Contains).ToArray());
            foreach(var cols in indexSets){using var index=memory.CreateCommand();index.CommandText="CREATE INDEX "+Quote("ask_idx_"+definition.Name+"_"+string.Join("_",cols))+" ON "+Quote(definition.Name)+" ("+string.Join(",",cols.Select(Quote))+")";index.ExecuteNonQuery();}
        }
        var snapshots=definitions.FirstOrDefault(d=>d.Name=="Snapshots");
        if(snapshots.Columns!=null && new[]{"Id","ValuationDate","CapturedAt"}.All(n=>snapshots.Columns.Any(c=>c.Name==n))) {
            using var views=memory.CreateCommand();
            views.CommandText="""
            CREATE TABLE AskLatestSnapshots AS SELECT s.* FROM Snapshots s JOIN
              (SELECT Id,ROW_NUMBER() OVER (PARTITION BY AccountId,ValuationDate ORDER BY CapturedAt DESC,Id DESC) AS capture_rank FROM Snapshots) ranked
              ON ranked.Id=s.Id WHERE ranked.capture_rank=1;
            CREATE INDEX ask_latest_id ON AskLatestSnapshots(Id);
            CREATE INDEX ask_latest_account_date ON AskLatestSnapshots(AccountId,ValuationDate);
            CREATE TABLE AskCurrentSnapshot AS SELECT s.* FROM AskLatestSnapshots s JOIN
              (SELECT AccountId,MAX(ValuationDate) AS LatestDate FROM AskLatestSnapshots GROUP BY AccountId) latest
              ON latest.AccountId=s.AccountId AND latest.LatestDate=s.ValuationDate;
            CREATE INDEX ask_current_id ON AskCurrentSnapshot(Id);
            CREATE INDEX ask_current_account ON AskCurrentSnapshot(AccountId);
            """;
            views.ExecuteNonQuery();
            tables.Add(new{table="AskLatestSnapshots",columns=snapshots.Columns.Select(c=>new{name=c.Name,type=c.Type}),note="Preferred: one newest capture for each account and valuation date."});
            tables.Add(new{table="AskCurrentSnapshot",columns=snapshots.Columns.Select(c=>new{name=c.Name,type=c.Type}),note="Preferred: the latest valuation snapshot per account."});
            var holdings=definitions.FirstOrDefault(d=>d.Name=="Holdings");
            if(holdings.Columns!=null) {
                using var hv=memory.CreateCommand();
                hv.CommandText="""
                CREATE VIEW AskLatestHoldings AS SELECT h.* FROM Holdings h JOIN AskLatestSnapshots s ON s.Id=h.SnapshotId WHERE NOT is_summary(h.Name,COALESCE(h.Symbol,''));
                CREATE VIEW AskCurrentHoldings AS SELECT h.* FROM Holdings h JOIN AskCurrentSnapshot s ON s.Id=h.SnapshotId WHERE NOT is_summary(h.Name,COALESCE(h.Symbol,''));
                """;
                hv.ExecuteNonQuery();
                tables.Add(new{table="AskLatestHoldings",columns=holdings.Columns.Select(c=>new{name=c.Name,type=c.Type}),note="Preferred holdings history: newest captures, summary rows excluded; join AskLatestSnapshots for dates."});
                tables.Add(new{table="AskCurrentHoldings",columns=holdings.Columns.Select(c=>new{name=c.Name,type=c.Type}),note="Preferred current holdings only, summary rows excluded."});
            }
        }
        var holdingsDefinition=definitions.FirstOrDefault(d=>d.Name=="Holdings");
        if(holdingsDefinition.Columns!=null && new[]{"Name","Symbol"}.All(n=>holdingsDefinition.Columns.Any(c=>c.Name==n))) {
            using var catalog=memory.CreateCommand();
            catalog.CommandText="SELECT DISTINCT Name,COALESCE(Symbol,'') FROM Holdings WHERE NOT is_summary(Name,COALESCE(Symbol,'')) ORDER BY Name LIMIT 200";
            using var cr=catalog.ExecuteReader();while(cr.Read())securities.Add(new{name=cr.GetString(0),symbol=cr.GetString(1)});
        }
        var events=definitions.FirstOrDefault(d=>d.Name=="SecurityHistoryEvents");
        if(events.Columns!=null) {
            using var view=memory.CreateCommand();
            view.CommandText="CREATE VIEW AskFirstOwned AS SELECT *,ToDate AS FirstOwnedDate,ToDate AS Date FROM SecurityHistoryEvents WHERE lower(trim(EventType))='first owned' AND NOT is_summary(Name,COALESCE(Symbol,''))";
            view.ExecuteNonQuery();
            tables.Add(new{table="AskFirstOwned",same_columns_as="SecurityHistoryEvents",extra_columns=new[]{"FirstOwnedDate (ToDate)","Date (ToDate)"},note="Explicit First owned records only, all recorded dates; not proof of actual purchase transaction dates."});
            using var markers=memory.CreateCommand();
            markers.CommandText="SELECT Name,COALESCE(Symbol,''),FromDate,ToDate,Source,AccountId FROM AskFirstOwned ORDER BY ToDate LIMIT 100";
            using(var mr=markers.ExecuteReader())while(mr.Read())ownershipMarkers.Add(new{name=mr.GetString(0),symbol=mr.GetString(1),from_date=mr.GetString(2),to_date=mr.GetString(3),source=mr.GetString(4),account_id=mr.GetInt64(5),account_name=accountNames.GetValueOrDefault(mr.GetInt64(5),"Account "+mr.GetInt64(5)),event_type="First owned"});
            using var types=memory.CreateCommand();types.CommandText="SELECT DISTINCT EventType FROM SecurityHistoryEvents ORDER BY EventType";
            using var tr=types.ExecuteReader();while(tr.Read())eventTypes.Add(tr.GetString(0));
        }
        using(var queryOnly=memory.CreateCommand()){queryOnly.CommandText="PRAGMA query_only=ON";queryOnly.ExecuteNonQuery();}
        raw.sqlite3_limit(memory.Handle,raw.SQLITE_LIMIT_LENGTH,2_000_000);
        raw.sqlite3_limit(memory.Handle,raw.SQLITE_LIMIT_SQL_LENGTH,20_000);
        raw.sqlite3_limit(memory.Handle,raw.SQLITE_LIMIT_ATTACHED,0);
        if(raw.sqlite3_set_authorizer(memory.Handle,(strdelegate_authorizer)((_,action,a,b,db,origin)=> {
            if(action==raw.SQLITE_SELECT || action==raw.SQLITE_READ || action==raw.SQLITE_RECURSIVE) return raw.SQLITE_OK;
            if(action==raw.SQLITE_FUNCTION && Functions.Contains(b??a??"")) return raw.SQLITE_OK;
            return raw.SQLITE_DENY;
        }),null)!=raw.SQLITE_OK)throw new InvalidOperationException("Could not install read-only SQL authorizer.");
        } catch {memory.Dispose();throw;}
    }
    public object Schema()=>new {account_id=AccountId==-1?(long?)null:AccountId,scope=AccountId==-1?"All accounts explicitly selected":"Selected account only",tables,available_accounts=accountNames.Select(p=>new{id=p.Key,name=p.Value}),security_catalog=securities,recorded_first_owned=ownershipMarkers,event_types=eventTypes,warnings=Warnings,
        note="Only the explicitly selected scope is present; All accounts is available only when selected in the UI. These are temporary RAM copies; source file is read-only. Summary rows can appear in Holdings: exclude exact total labels when analyzing individual securities. Source event labels are not proof of purchase transactions."};
    public List<string> OwnershipFacts(string question) {
        var facts=new List<string>();
        foreach(var item in ownershipMarkers) {
            var record=JsonSerializer.SerializeToElement(item);
            var name=record.GetProperty("name").GetString()??"";var symbol=record.GetProperty("symbol").GetString()??"";
            bool symbolMatch=symbol.Length>=2 && System.Text.RegularExpressions.Regex.IsMatch(question,@"(?<!\w)"+System.Text.RegularExpressions.Regex.Escape(symbol)+@"(?!\w)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var words=System.Text.RegularExpressions.Regex.Matches(name,@"[\p{L}]+").Select(m=>m.Value).Where(w=>w.Length>=3&&!OwnershipStopWords.Contains(w));
            bool nameMatch=words.Any(w=>System.Text.RegularExpressions.Regex.IsMatch(question,@"(?<!\w)"+System.Text.RegularExpressions.Regex.Escape(w)+@"(?!\w)",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            if(symbolMatch||nameMatch) {
                var from=record.GetProperty("from_date").GetString();var to=record.GetProperty("to_date").GetString();
                facts.Add("Account "+record.GetProperty("account_id").GetInt64()+" ("+record.GetProperty("account_name").GetString()+"): "+name+" ("+symbol+") — Recorded first owned: "+(from==to?to:from+" – "+to)+"; source: "+record.GetProperty("source").GetString()+". This is a recorded ownership event, not proof of an actual purchase transaction date.");
            }
        }
        return facts;
    }
    public QueryData Query(string sql,CancellationToken token=default) {
        token.ThrowIfCancellationRequested();
        if(string.IsNullOrWhiteSpace(sql)||sql.Length>20_000)throw new ArgumentException("SQL must contain a bounded read query.");
        var timer=Stopwatch.StartNew();
        raw.sqlite3_progress_handler(memory.Handle,1000,_=>token.IsCancellationRequested||timer.Elapsed>TimeSpan.FromSeconds(8)?1:0,null);
        try {
            using var cmd=memory.CreateCommand();cmd.CommandText=sql;cmd.CommandTimeout=10;
            using var registration=token.Register(()=>raw.sqlite3_interrupt(memory.Handle));
            using var reader=cmd.ExecuteReader();
            var columns=Enumerable.Range(0,reader.FieldCount).Select(reader.GetName).ToList();
            var rows=new List<object?[]>();bool truncated=false;int characters=0;
            while(reader.Read()) {
                token.ThrowIfCancellationRequested();
                if(rows.Count>=500 || characters>60_000) {truncated=true;break;}
                var row=new object?[reader.FieldCount];
                for(int i=0;i<row.Length;i++) {
                    object? value=reader.IsDBNull(i)?null:reader.GetValue(i);
                    if(value is byte[])value="[binary omitted]";
                    if(value is string text && text.Length>2000){value=text[..2000]+" [truncated]";truncated=true;}
                    row[i]=value;characters+=Convert.ToString(value)?.Length??0;
                }
                rows.Add(row);
            }
            // sqlite3 executes subsequent statements on NextResult; inspect them as well under the authorizer.
            if(reader.NextResult())throw new ArgumentException("Use one SELECT result per query.");
            return new(columns,rows,truncated);
        } catch(SqliteException ex) when(ex.SqliteErrorCode==9) {
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("The read-only query exceeded its execution budget. Completed evidence remains available; narrow or simplify this query.",ex);
        } finally {raw.sqlite3_progress_handler(memory.Handle,0,null,null);}
    }
    public void Dispose()=>memory.Dispose();
    private static string Quote(string name)=>((char)34)+name.Replace(((char)34).ToString(),new string((char)34,2))+((char)34);
    private static string Affinity(string type)=>type.ToUpperInvariant() switch {
        var t when t.Contains("INT")=>"INTEGER",var t when t.Contains("CHAR")||t.Contains("TEXT")||t.Contains("CLOB")=>"TEXT",
        var t when t.Contains("BLOB")=>"BLOB",_=>"REAL"
    };
    private static readonly HashSet<string> OwnershipStopWords=new(StringComparer.OrdinalIgnoreCase) {
        "INC","LTD","CORP","ETF","FUND","THE","CLASS","FIRST","TRUST","AND","FOR","WHEN","DID","BUY","BOUGHT","PURCHASE","PURCHASED",
        "DATE","ACCOUNT","PORTFOLIO","HOLDING","HOLDINGS","SECURITY","SECURITIES","HISTORY","ONLY","OWN","OWNED","OWNERSHIP","MY","PLEASE","SHOW","TELL","RECORDED","SHARES","COMPANY"
    };
    private static readonly HashSet<string> Functions=new(StringComparer.OrdinalIgnoreCase) {
        "is_summary","count","sum","total","avg","min","max","abs","round","coalesce","ifnull","nullif","iif",
        "lower","upper","trim","ltrim","rtrim","replace","substr","substring","length","instr","like","glob",
        "date","time","datetime","julianday","strftime","unixepoch","timediff",
        "lag","lead","first_value","last_value","nth_value","row_number","rank","dense_rank","percent_rank","cume_dist","ntile",
        "sqrt","pow","power","log","log10","ln","exp","ceil","ceiling","floor","trunc","mod",
        "json_extract","json_valid","json_array_length","json_type","json_each","json_tree","json_object","json_array","json_group_array","json_group_object","group_concat"
    };
}
public sealed record QueryData(List<string> Columns,List<object?[]> Rows,bool Truncated);





