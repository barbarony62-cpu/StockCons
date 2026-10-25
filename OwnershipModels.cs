namespace AskCons;
public sealed record RecordedHistoryEvent(DateTime FromDate,DateTime ToDate,string Name,string Symbol,
    double? FromQuantity,double? ToQuantity,string EventType,string Source);
public sealed record QuantityObservation(DateTime Date,double Quantity);
public sealed record OwnershipResult(string Security,List<RecordedHistoryEvent> Events);
public sealed class HistoryRow {
    public DateTime EventDate {get;init;}
    public string Period {get;init;}="";
    public string Event {get;init;}="";
    public string Quantity {get;init;}="";
    public string Source {get;init;}="";
}
