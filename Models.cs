namespace AskCons;
public sealed record Account(long Id,string Name);
public sealed record Valuation(DateTime Date,double Value);
public sealed class QueryIntent
{
 public bool first_recorded_start {get;set;}
 public string action {get;set;}="execute"; public string? clarification {get;set;} public string? interpretation {get;set;}
 public string operation {get;set;}="unsupported";
 public string? security {get;set;}
 public string? start_date {get;set;}
 public string? end_date {get;set;}
 public string? language {get;set;}
 public string? interval {get;set;}
 public int? limit {get;set;}
 public bool? present_at_start {get;set;}
 public bool? absent_at_end {get;set;}
 public double? minimum_change_nis {get;set;}
 public double? maximum_change_nis {get;set;}
 public string? direction {get;set;}
 public string? sort_by {get;set;}
 public string? security_type {get;set;}
 public string? presentation {get;set;}
 public string? name_contains {get;set;}
 public double? minimum_start_value_nis {get;set;}
 public double? maximum_start_value_nis {get;set;}
 public double? minimum_end_value_nis {get;set;}
 public double? maximum_end_value_nis {get;set;}
 public string? metric {get;set;}
 public string? entity {get;set;}
 public string? aggregation {get;set;}
 public string? group_by {get;set;}
 public string? sort_direction {get;set;}
 public bool? require_present_at_end {get;set;}
 public string? unsupported_reason {get;set;}
}
public sealed record HoldingChange(string Name,string Symbol,double StartValue,double EndValue,double Change,bool PresentAtStart=true,bool PresentAtEnd=true);
public sealed record HoldingRanking(DateTime StartDate,DateTime EndDate,List<HoldingChange> Items);
public sealed record TimePoint(DateTime Date,double Value);
public sealed record SeriesResult(string Label, string Interval, List<TimePoint> Points,string Unit="NIS");
public sealed class RankingRow { public string SecurityKey {get;set;}=""; public int Rank {get;set;} public string Security {get;set;}=""; public double Start {get;set;} public double End {get;set;} public double Change {get;set;} }
public sealed class SeriesRow { public string Period {get;set;}=""; public DateTime Date {get;set;} public double Value {get;set;} }




