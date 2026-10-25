using Microsoft.Data.Sqlite;
namespace AskCons;
public sealed partial class ReadOnlyPortfolio
{
    private static bool HasTable(SqliteConnection c,string table) {
        using var cmd=c.CreateCommand(); cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$t";
        cmd.Parameters.AddWithValue("$t",table); return Convert.ToInt64(cmd.ExecuteScalar())>0;
    }
    public List<RecordedHistoryEvent> StoredHistory(long accountId,SecurityCandidate security,DateTime? from=null,DateTime? to=null) {
        using var c=Connect();
        if(!HasTable(c,"SecurityHistoryEvents"))
            throw new InvalidOperationException("This database has no SecurityHistoryEvents table. No first-owned date can be established from stored history.");
        using var cmd=c.CreateCommand();
        cmd.CommandText="""
        SELECT FromDate,ToDate,Name,COALESCE(Symbol,''),FromQuantity,ToQuantity,EventType,Source
        FROM SecurityHistoryEvents
        WHERE AccountId=$a AND NOT askcons_summary(Name,COALESCE(Symbol,''))
        AND ((COALESCE(Symbol,'')<>'' AND UPPER(Symbol)=UPPER($s))
          OR (COALESCE(Symbol,'')='' AND UPPER(Name)=UPPER($n)))
        AND ($from IS NULL OR ToDate >= $from) AND ($to IS NULL OR FromDate <= $to)
        ORDER BY ToDate,FromDate,Id
        """;
        cmd.Parameters.AddWithValue("$a",accountId); cmd.Parameters.AddWithValue("$s",security.Symbol);
        cmd.Parameters.AddWithValue("$n",security.Name);
        cmd.Parameters.AddWithValue("$from",(object?)from?.ToString("yyyy-MM-dd")??DBNull.Value);
        cmd.Parameters.AddWithValue("$to",(object?)to?.ToString("yyyy-MM-dd")??DBNull.Value);
        using var r=cmd.ExecuteReader(); var result=new List<RecordedHistoryEvent>();
        while(r.Read()) result.Add(new(DateTime.Parse(r.GetString(0)),DateTime.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),
            r.IsDBNull(4)?null:r.GetDouble(4),r.IsDBNull(5)?null:r.GetDouble(5),r.GetString(6),r.GetString(7)));
        return result;
    }
    public List<QuantityObservation> ObservedQuantities(long accountId,SecurityCandidate security,DateTime? from=null,DateTime? to=null) {
        using var c=Connect();
        using(var info=c.CreateCommand()) {
            info.CommandText="PRAGMA table_info(Holdings)"; using var reader=info.ExecuteReader(); bool quantity=false;
            while(reader.Read()) quantity |= reader.GetString(1).Equals("Quantity",StringComparison.OrdinalIgnoreCase);
            if(!quantity) return new();
        }
        using var cmd=c.CreateCommand(); cmd.CommandText="""
        SELECT s.ValuationDate,
          (SELECT CASE WHEN COUNT(*)=COUNT(h.Quantity) THEN SUM(h.Quantity) ELSE NULL END
           FROM Holdings h WHERE h.SnapshotId=s.Id AND NOT askcons_summary(h.Name,COALESCE(h.Symbol,''))
             AND ((COALESCE(h.Symbol,'')<>'' AND UPPER(h.Symbol)=UPPER($s))
               OR (COALESCE(h.Symbol,'')='' AND UPPER(h.Name)=UPPER($n))))
        FROM Snapshots s WHERE s.AccountId=$a
        AND ($from IS NULL OR s.ValuationDate >= $from) AND ($to IS NULL OR s.ValuationDate <= $to)
        ORDER BY s.ValuationDate DESC,s.CapturedAt DESC,s.Id DESC
        """;
        cmd.Parameters.AddWithValue("$a",accountId); cmd.Parameters.AddWithValue("$s",security.Symbol); cmd.Parameters.AddWithValue("$n",security.Name);
        cmd.Parameters.AddWithValue("$from",(object?)from?.ToString("yyyy-MM-dd")??DBNull.Value);
        cmd.Parameters.AddWithValue("$to",(object?)to?.ToString("yyyy-MM-dd")??DBNull.Value);
        using var r=cmd.ExecuteReader(); var dates=new HashSet<DateTime>(); var result=new List<QuantityObservation>();
        while(r.Read()) {
            var d=DateTime.Parse(r.GetString(0));
            if(!dates.Add(d) || r.IsDBNull(1)) continue;
            result.Add(new(d,r.GetDouble(1)));
        }
        return result.OrderBy(x=>x.Date).ToList();
    }
}

