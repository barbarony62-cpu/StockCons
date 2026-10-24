namespace AskCons;
public sealed record Account(long Id,string Name);
public sealed record Valuation(DateTime Date,double Value);
public sealed class QueryIntent { public string operation {get;set;}="unsupported"; public string? security {get;set;} public string? start_date {get;set;} public string? end_date {get;set;} public string? language {get;set;} }
