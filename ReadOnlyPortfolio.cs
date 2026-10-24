using System.IO;
using Microsoft.Data.Sqlite;
namespace AskCons;
public sealed class ReadOnlyPortfolio {
 public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Consolidation","consolidation.db");
 private readonly string path;
 public ReadOnlyPortfolio(string? path=null){this.path=path??DefaultPath;}
 private SqliteConnection Connect(){if(!File.Exists(path))throw new FileNotFoundException("Consolidation database was not found",path);var cs=new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Cache=SqliteCacheMode.Private,Pooling=false}.ToString();var c=new SqliteConnection(cs);c.Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA query_only=ON";cmd.ExecuteNonQuery();return c;}
 public List<Account> Accounts(){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,FriendlyName FROM Accounts ORDER BY Id";using var rd=cmd.ExecuteReader();var result=new List<Account>();while(rd.Read())result.Add(new Account(rd.GetInt64(0),rd.GetString(1)));return result;}
 public Valuation? AccountValue(long id,DateTime date){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="SELECT ValuationDate,LiquidAssetsIls FROM Snapshots WHERE AccountId=$a AND ValuationDate <= $d AND LiquidAssetsIls IS NOT NULL ORDER BY ValuationDate DESC, CapturedAt DESC, Id DESC LIMIT 1";cmd.Parameters.AddWithValue("$a",id);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));using var rd=cmd.ExecuteReader();return rd.Read()?new Valuation(DateTime.Parse(rd.GetString(0)),rd.GetDouble(1)):null;}
 public Valuation? HoldingValue(long id,string term,DateTime date){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="""
 SELECT s.ValuationDate, SUM(h.ValueIls) FROM Snapshots s JOIN Holdings h ON h.SnapshotId=s.Id
 WHERE s.Id=(SELECT s2.Id FROM Snapshots s2 WHERE s2.AccountId=$a AND s2.ValuationDate<=$d ORDER BY s2.ValuationDate DESC,s2.CapturedAt DESC,s2.Id DESC LIMIT 1)
 AND (LOWER(h.Name) LIKE $q OR LOWER(COALESCE(h.Symbol,''))=$symbol)
 AND h.ValueIls IS NOT NULL GROUP BY s.ValuationDate
 """;cmd.Parameters.AddWithValue("$a",id);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$q","%"+term.ToLowerInvariant().Replace("%", "") .Replace("_", "")+"%");cmd.Parameters.AddWithValue("$symbol",term.ToLowerInvariant());using var rd=cmd.ExecuteReader();return rd.Read()?new Valuation(DateTime.Parse(rd.GetString(0)),rd.GetDouble(1)):null;}
    // Read two complete, dated snapshots. Missing holdings count as zero, not missing snapshots.
    public HoldingRanking? RankHoldingChanges(long accountId, DateTime requestedStart, DateTime requestedEnd)
    {
        using var c = Connect();
        (long id, DateTime date)? SnapshotAt(DateTime date)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, ValuationDate FROM Snapshots WHERE AccountId=$a AND ValuationDate <= $d ORDER BY ValuationDate DESC, CapturedAt DESC, Id DESC LIMIT 1";
            cmd.Parameters.AddWithValue("$a", accountId);
            cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
            using var r = cmd.ExecuteReader();
            return r.Read() ? (r.GetInt64(0), DateTime.Parse(r.GetString(1))) : null;
        }
        var first = SnapshotAt(requestedStart);
        var last = SnapshotAt(requestedEnd);
        if (first is null || last is null) return null;
        Dictionary<string,(string name,string symbol,double value)> ReadHoldings(long snapshotId)
        {
            var data = new Dictionary<string,(string,string,double)>(StringComparer.OrdinalIgnoreCase);
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Name, COALESCE(Symbol,''), ValueIls FROM Holdings WHERE SnapshotId=$s AND ValueIls IS NOT NULL";
            cmd.Parameters.AddWithValue("$s", snapshotId);
            using var r = cmd.ExecuteReader();
            while(r.Read())
            {
                var name = r.GetString(0).Trim();
                var symbol = r.GetString(1).Trim();
                // Match by symbol where available, otherwise by the recorded holding name.
                var key = symbol.Length > 0 ? "S:"+symbol.ToUpperInvariant() : "N:"+name.ToUpperInvariant();
                var value = r.GetDouble(2);
                if (data.TryGetValue(key, out var old)) value += old.value;
                data[key] = (name, symbol, value);
            }
            return data;
        }
        var start = ReadHoldings(first.Value.id);
        var end = ReadHoldings(last.Value.id);
        var keys = start.Keys.Union(end.Keys, StringComparer.OrdinalIgnoreCase);
        var items = keys.Select(key =>
        {
            var hasStart = start.TryGetValue(key, out var a);
            var hasEnd = end.TryGetValue(key, out var b);
            var startValue = hasStart ? a.value : 0d;
            var endValue = hasEnd ? b.value : 0d;
            return new HoldingChange(hasEnd ? b.name : a.name, hasEnd ? b.symbol : a.symbol,
                startValue, endValue, endValue-startValue);
        }).ToList();
        return new HoldingRanking(first.Value.date, last.Value.date, items);
    }
}
