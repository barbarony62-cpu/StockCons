using System.IO;
using Microsoft.Data.Sqlite;
namespace AskCons;
public sealed class ReadOnlyPortfolio {
 public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Consolidation","consolidation.db");
 private readonly string path;
 public ReadOnlyPortfolio(string? path=null){this.path=path??DefaultPath;}
 private SqliteConnection Connect(){if(!File.Exists(path))throw new FileNotFoundException("Consolidation database was not found",path);var cs=new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Cache=SqliteCacheMode.Private,Pooling=false}.ToString();var c=new SqliteConnection(cs);c.Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA query_only=ON";cmd.ExecuteNonQuery();return c;}
 public List<SecurityCandidate> ResolveSecurity(long accountId, string term) {
    using var c=Connect(); using var cmd=c.CreateCommand();
    cmd.CommandText="SELECT DISTINCT h.Name, COALESCE(h.Symbol,'') FROM Holdings h JOIN Snapshots s ON s.Id=h.SnapshotId WHERE s.AccountId=$a";
    cmd.Parameters.AddWithValue("$a",accountId);
    using var r=cmd.ExecuteReader(); var all=new List<SecurityCandidate>();
    while(r.Read()) all.Add(new(r.GetString(0),r.GetString(1)));
    var exact=all.Where(x=>string.Equals(x.Name,term,StringComparison.OrdinalIgnoreCase) || string.Equals(x.Symbol,term,StringComparison.OrdinalIgnoreCase)).ToList();
    var matches=exact.Count>0?exact:all.Where(x=>x.Name.Contains(term,StringComparison.OrdinalIgnoreCase) || x.Symbol.Contains(term,StringComparison.OrdinalIgnoreCase)).ToList();
    return matches.GroupBy(x=>string.IsNullOrWhiteSpace(x.Symbol)?"N:"+x.Name:"S:"+x.Symbol,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
 } public (Valuation Holding, Valuation Account)? HoldingShare(long id,string term,DateTime date) {
    using var c=Connect(); using var cmd=c.CreateCommand();
    cmd.CommandText="""
    SELECT s.ValuationDate, s.LiquidAssetsIls,
      (SELECT SUM(h.ValueIls) FROM Holdings h WHERE h.SnapshotId=s.Id
        AND (LOWER(h.Name)=$q OR LOWER(COALESCE(h.Symbol,''))=$q))
    FROM Snapshots s WHERE s.AccountId=$a AND s.ValuationDate<=$d
    ORDER BY s.ValuationDate DESC,s.CapturedAt DESC,s.Id DESC LIMIT 1
    """;
    cmd.Parameters.AddWithValue("$a",id); cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));
    cmd.Parameters.AddWithValue("$q",term.ToLowerInvariant());
    using var r=cmd.ExecuteReader();
    return r.Read() && !r.IsDBNull(1) && !r.IsDBNull(2) ?
        (new Valuation(DateTime.Parse(r.GetString(0)),r.GetDouble(2)),new Valuation(DateTime.Parse(r.GetString(0)),r.GetDouble(1))) : null;
 }
 public List<Account> Accounts(){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,FriendlyName FROM Accounts ORDER BY Id";using var rd=cmd.ExecuteReader();var result=new List<Account>();while(rd.Read())result.Add(new Account(rd.GetInt64(0),rd.GetString(1)));return result;}
 public Valuation? AccountValue(long id,DateTime date){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="SELECT ValuationDate,LiquidAssetsIls FROM Snapshots WHERE AccountId=$a AND ValuationDate <= $d ORDER BY ValuationDate DESC, CapturedAt DESC, Id DESC LIMIT 1";cmd.Parameters.AddWithValue("$a",id);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));using var rd=cmd.ExecuteReader();return rd.Read() && !rd.IsDBNull(1)?new Valuation(DateTime.Parse(rd.GetString(0)),rd.GetDouble(1)):null;}
 public Valuation? HoldingValue(long id,string term,DateTime date){using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="""
 SELECT s.ValuationDate, SUM(h.ValueIls) FROM Snapshots s JOIN Holdings h ON h.SnapshotId=s.Id
 WHERE s.Id=(SELECT s2.Id FROM Snapshots s2 WHERE s2.AccountId=$a AND s2.ValuationDate<=$d ORDER BY s2.ValuationDate DESC,s2.CapturedAt DESC,s2.Id DESC LIMIT 1)
 AND (LOWER(h.Name) = $q OR LOWER(COALESCE(h.Symbol,''))=$symbol)
 AND h.ValueIls IS NOT NULL GROUP BY s.ValuationDate
 """;cmd.Parameters.AddWithValue("$a",id);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$q",term.ToLowerInvariant());cmd.Parameters.AddWithValue("$symbol",term.ToLowerInvariant());using var rd=cmd.ExecuteReader();return rd.Read() && !rd.IsDBNull(1)?new Valuation(DateTime.Parse(rd.GetString(0)),rd.GetDouble(1)):null;}
    // The earliest recorded snapshot within the requested calendar period.
    public DateTime? FirstSnapshotDate(long accountId, DateTime from, DateTime to)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT MIN(ValuationDate) FROM Snapshots WHERE AccountId=$a AND ValuationDate >= $from AND ValuationDate <= $to";
        cmd.Parameters.AddWithValue("$a", accountId);
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        var result = cmd.ExecuteScalar();
        return result is string date ? DateTime.Parse(date) : null;
    }
    // Read two complete, dated snapshots. Missing holdings count as zero, not missing snapshots.
    public HoldingRanking? RankHoldingChanges(long accountId, DateTime requestedStart, DateTime requestedEnd, bool openingHoldingsOnly = false)
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
            var data = new Dictionary<string,(string name,string symbol,double value)>(StringComparer.OrdinalIgnoreCase);
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Name, COALESCE(Symbol,''), ValueIls FROM Holdings WHERE SnapshotId=$s";
            cmd.Parameters.AddWithValue("$s", snapshotId);
            using var r = cmd.ExecuteReader();
            while(r.Read())
            {
                var name = r.GetString(0).Trim();
                var symbol = r.GetString(1).Trim();
                // Match by symbol where available, otherwise by the recorded holding name.
                var key = symbol.Length > 0 ? "S:"+symbol.ToUpperInvariant() : "N:"+name.ToUpperInvariant();
                if (r.IsDBNull(2)) throw new InvalidOperationException("A holding valuation is missing; the snapshots cannot be compared safely.");
                var value = r.GetDouble(2);
                if (data.TryGetValue(key, out var old)) value += old.value;
                data[key] = (name, symbol, value);
            }
            return data;
        }
        var start = ReadHoldings(first.Value.id);
        var end = ReadHoldings(last.Value.id);
        var keys = openingHoldingsOnly ? start.Keys : start.Keys.Union(end.Keys, StringComparer.OrdinalIgnoreCase);
        var items = keys.Select(key =>
        {
            var hasStart = start.TryGetValue(key, out var a);
            var hasEnd = end.TryGetValue(key, out var b);
            var startValue = hasStart ? a.value : 0d;
            var endValue = hasEnd ? b.value : 0d;
            return new HoldingChange(hasEnd ? b.name : a.name, hasEnd ? b.symbol : a.symbol,
                startValue, endValue, endValue-startValue, hasStart, hasEnd);
        }).ToList();
        return new HoldingRanking(first.Value.date, last.Value.date, items);
    }

    // An exact snapshot is required for security presence checks; never confuse absence with zero.
    public List<HoldingChange>? CurrentHoldings(long accountId, DateTime date)
    {
        var ranking = RankHoldingChanges(accountId, date, date);
        return ranking?.Items;
    }
    public double? AccountTotalAt(long accountId, DateTime date) => AccountValue(accountId,date)?.Value;

    // Only SELECT queries. One latest snapshot per valuation date, then the last available
    // observation in each requested calendar bucket. No interpolation or fabricated values.
    public SeriesResult? HistoricalSeries(long accountId, string? security, DateTime from, DateTime to, string interval)
    {
        using var c = Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = security is null ? """
            SELECT s.ValuationDate, s.LiquidAssetsIls FROM Snapshots s
            WHERE s.AccountId=$a AND s.ValuationDate >= $from AND s.ValuationDate <= $to

            ORDER BY s.ValuationDate DESC, s.CapturedAt DESC, s.Id DESC
            """ : """
            SELECT s.ValuationDate,
              (SELECT SUM(h.ValueIls) FROM Holdings h WHERE h.SnapshotId=s.Id
                AND (LOWER(h.Name) = $term OR LOWER(COALESCE(h.Symbol,''))=$symbol))
            FROM Snapshots s
            WHERE s.AccountId=$a AND s.ValuationDate >= $from AND s.ValuationDate <= $to
            ORDER BY s.ValuationDate DESC, s.CapturedAt DESC, s.Id DESC
            """;
        cmd.Parameters.AddWithValue("$a",accountId);
        cmd.Parameters.AddWithValue("$from",from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to",to.ToString("yyyy-MM-dd"));
        if (security is not null)
        {
            cmd.Parameters.AddWithValue("$term",security.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$symbol",security.ToLowerInvariant());
        }
        var dates = new HashSet<DateTime>();
        var buckets = new Dictionary<DateTime,TimePoint>();
        using var reader = cmd.ExecuteReader();
        while(reader.Read())
        {
            var date = DateTime.Parse(reader.GetString(0));
            if (!dates.Add(date)) continue; // newest capture wins for each date
            if (reader.IsDBNull(1)) continue; // Absence or missing valuation is not a zero price.
            var value = reader.GetDouble(1);
            DateTime bucket = interval switch
            {
                "week" => date.Date.AddDays((6-(int)date.DayOfWeek+7)%7), // Saturday
                "day" => date.Date,
                _ => new DateTime(date.Year,date.Month,1)
            };
            if (!buckets.ContainsKey(bucket)) buckets[bucket] = new TimePoint(date,value);
        }
        if (buckets.Count == 0) return null;
        var points = buckets.OrderBy(x=>x.Key).Select(x=>x.Value).ToList();
        return new SeriesResult(security is null ? "Account value" : security,interval,points);
    }
}





