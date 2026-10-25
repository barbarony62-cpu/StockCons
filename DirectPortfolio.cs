using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
namespace AskCons;
public sealed record DirectAnswer(AnswerData Answer,string Details);
public static class ResearchPolicy {
 public static bool Relevant(string question)=>Regex.IsMatch(question,@"\b(web|news|research|outlook|valuation|earnings|fundamental|business|forecast|better|prefer|compare|why|recommend|p/e|analys[ei]s of|tell me about)\b",RegexOptions.IgnoreCase);
}
public static class SecurityClassification {
 public static string? Classify(string? raw)=>raw?.Trim().ToLowerInvariant() switch {
  "stock" or "stocks" or "equity" or "equities" or "share" or "shares" or "מניה" or "מניות"=>"stock",
  "fund" or "mutual fund" or "etf" or "קרן" or "קרן נאמנות" or "קרן סל"=>"fund",
  "bond" or "bonds" or "אגח" or "אג\"ח" or "אגרת חוב" or "איגרת חוב"=>"bond",_=>null};
}
public static class DirectPortfolio {
 static bool Has(string q,string pattern)=>Regex.IsMatch(q,pattern,RegexOptions.IgnoreCase);
 public static string? Route(string question) {
  if(Has(question,@"interpolat|extrapolat|forecast|scenario|what if|assum|compound|correlat"))return null;
  if(Has(question,@"first.*(?:buy|bought|own|purchas)|when.*(?:buy|bought)"))return "first_owned";
  if(Has(question,@"month.by.month|monthly|each month|every month|per month")&&Has(question,@"portfolio|account|worth|value|change|gain|loss|lost"))return "monthly";
  if(Has(question,@"allocation|concentrat|portfolio weights?|holdings weights?|share of.*(?:portfolio|account)"))return "allocation";
  if(Has(question,@"largest|top\s+\d+|biggest|gainers|losers|best|worst")&&Has(question,@"holding|securit|stock|position")&&Has(question,@"value|change|gain|loss|largest|biggest|gainers|losers"))return "ranking";
  if(Has(question,@"portfolio|account")&&Has(question,@"value|worth|change|gain|loss")&&!Has(question,@"why|explain|better|risk|diversif"))return "valuation";
  return null;
 }
 public static DirectAnswer? TryCalculate(string question,Account account,string path,CancellationToken token) {
  var route=Route(question);if(route==null)return null;
  // A narrow deterministic route never silently ignores a security-specific or complex date request.
  if(route=="monthly"&&Has(question,@"\b(stock|fund|bond|tesla|apple)\b|\[ID:"))return null;
  if(Has(question,@"last month|last week|quarter|past \d+|last \d+|weekly|daily|net worth|inflation|adjusted|deposit|withdraw|dividend|profit after|since inception"))return null;
  var clock=Stopwatch.StartNew();var log=new StringBuilder("ORIGINAL QUESTION\n"+question+"\nACCOUNT\n"+account.Name+" ("+account.Id+")\nROUTE\nDeterministic local "+route+"; AI and web not required.\n");
  using var sandbox=new AnalysisSandbox(path,account.Id,token);
  QueryData Read(string sql){var t=Stopwatch.StartNew();try{var data=sandbox.Query(sql,token);log.AppendLine("SQL\n"+sql+"\nRESULT rows="+data.Rows.Count+"; partial="+data.Truncated+"; elapsed_ms="+t.ElapsedMilliseconds+"\n"+JsonSerializer.Serialize(data));return data;}catch(Exception ex){log.AppendLine("SQL FAILURE\n"+sql+"\n"+ex);ex.Data["AskConsDiagnostics"]=log.ToString();throw;}}
  var profile="";bool hasType=false;
  try{var types=Read("SELECT COALESCE(SecurityType,'[missing]') AS SecurityType,COUNT(*) AS Rows FROM AskCurrentHoldings GROUP BY SecurityType");hasType=true;profile=string.Join("; ",types.Rows.Select(r=>r[0]+"="+r[1]));}catch(Microsoft.Data.Sqlite.SqliteException){profile="SecurityType column is unavailable.";}
  var warnings=new List<string>(sandbox.Warnings.Where(w=>w.StartsWith("PARTIAL")));
  log.AppendLine("CLASSIFICATION PROFILE\n"+profile);
  string note="Recorded value change is not investment performance: deposits, withdrawals, trades, dividends and FX are not adjusted. Missing observations are not invented.";
  var table=new DataTable();
  DirectAnswer Finish(string title,string text,string extra="") {
   var report=text+"\n\n"+note+(extra==""?"":"\n"+extra)+(warnings.Count==0?"":"\n"+string.Join("\n",warnings));
   log.AppendLine("STAGE COMPLETED\nC# calculation and formatting; total_ms="+clock.ElapsedMilliseconds+"\nWEB\nSkipped: request answered from local records.");
   object display=table;
   if(route=="monthly"&&Has(question,"chart|graph|plot")&&table.Rows.Count>0&&table.Rows.Cast<DataRow>().Select(r=>r["Portfolio"].ToString()).Distinct().Count()==1) {
      bool delta=Has(question,"change|gain|loss|lost");var points=new List<TimePoint>();
      foreach(DataRow r in table.Rows)if(double.TryParse(Convert.ToString(r[delta?"Change NIS":"Closing NIS"]),NumberStyles.Number,CultureInfo.CurrentCulture,out var value))points.Add(new(DateTime.Parse(Convert.ToString(r["Closing recorded date"])!,CultureInfo.InvariantCulture),value));
      if(points.Count>0)display=new SeriesResult(delta?"Monthly recorded value change":"Recorded month-end value","month",points);
   }
   return new(new(display,account.Name+" · "+title,note+" "+extra+(warnings.Count==0?"":" "+string.Join(" ",warnings.Distinct())),report),log.ToString());
  }
  if(route=="first_owned") {
   var subjects=ResearchSelection.Resolve(question,[]);var matches=Regex.Matches(question,@"\[ID:\s*([^\]]+)\]").Select(m=>m.Groups[1].Value).Concat(subjects.Select(s=>s.Symbol)).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
   if(matches.Length!=1)return null;
   var key=matches[0].Replace("'","''");var data=Read("SELECT AccountId,Name,Symbol,FirstOwnedDate,Source FROM AskFirstOwned WHERE upper(Symbol)=upper('"+key+"') ORDER BY FirstOwnedDate");
   if(data.Truncated)warnings.Add("Ownership results are partial.");
   foreach(var c in data.Columns)table.Columns.Add(c);foreach(var r in data.Rows)table.Rows.Add(r);
   note="Recorded First owned events are not proof of actual purchase transaction dates.";
   return Finish("Recorded first owned",Format(table),data.Rows.Count==0?"No matching explicit ownership event was found; this does not establish that the security was never owned.":"");
  }
  var maxDate=Read("SELECT MAX(ValuationDate) AS LatestDate FROM AskLatestSnapshots");
  if(maxDate.Rows.Count==0||maxDate.Rows[0][0]==null)return Finish("No recorded values","No recorded portfolio dates are available.");
  var latest=DateTime.Parse(Convert.ToString(maxDate.Rows[0][0])!,CultureInfo.InvariantCulture);
  var yearMatch=Regex.Match(question,@"\b(19\d{2}|20\d{2})\b");var year=yearMatch.Success?int.Parse(yearMatch.Value):Has(question,"last year")?DateTime.Today.Year-1:Has(question,"this year|today")?DateTime.Today.Year:latest.Year;
  var dates=Regex.Matches(question,@"\b\d{4}-\d{2}-\d{2}\b").Select(m=>DateTime.TryParseExact(m.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?(DateTime?)d:null).Where(d=>d.HasValue).Select(d=>d!.Value).ToArray();
  var englishDates=Regex.Matches(question,@"\b(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(\d{1,2})(?:st|nd|rd|th)?(?:,?\s+((?:19|20)\d{2}))?",RegexOptions.IgnoreCase);
  if(dates.Length==0&&englishDates.Count>0){var parsed=new List<DateTime>();foreach(Match m in englishDates){var y=m.Groups[3].Success?int.Parse(m.Groups[3].Value):year;if(!DateTime.TryParse(m.Groups[1].Value+" "+m.Groups[2].Value+" "+y,CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))return null;parsed.Add(d);}dates=parsed.ToArray();}
  if(Has(question,@"\btoday\b"))dates=dates.Append(DateTime.Today).ToArray();
  if(Has(question,@"\b(from|since|between)\b")&&dates.Length==0&&!Has(question,@"(?:jan(?:uary)?\s+1|first day|start of).*year"))return null;
  var openEnded=dates.Length==1&&Has(question,@"\b(from|since)\b")&&!Has(question,@"\b(to|until|through)\b");
  var start=dates.Length>=2||openEnded?dates[0]:new DateTime(year,1,1);var end=openEnded?latest:dates.Length>=1?dates[^1]:new DateTime(year,12,31);
  if(end<start)return Finish("Invalid date range","The ending date precedes the starting date. Please correct the requested range.");
  string before="SELECT x.Id FROM AskLatestSnapshots x WHERE x.AccountId=s.AccountId AND x.LiquidAssetsIls IS NOT NULL AND x.ValuationDate<'"+start.ToString("yyyy-MM-dd")+"' ORDER BY x.ValuationDate DESC LIMIT 1";
  string openingSql="SELECT x.Id FROM AskLatestSnapshots x WHERE x.AccountId=s.AccountId AND x.LiquidAssetsIls IS NOT NULL AND x.ValuationDate>='"+start.ToString("yyyy-MM-dd")+"' AND x.ValuationDate<='"+end.ToString("yyyy-MM-dd")+"' ORDER BY x.ValuationDate LIMIT 1";
  string closingSql="SELECT x.Id FROM AskLatestSnapshots x WHERE x.AccountId=s.AccountId AND x.LiquidAssetsIls IS NOT NULL AND x.ValuationDate<='"+end.ToString("yyyy-MM-dd")+"' ORDER BY x.ValuationDate DESC LIMIT 1";
  string monthEnds="SELECT x.Id FROM AskLatestSnapshots x WHERE x.AccountId=s.AccountId AND substr(x.ValuationDate,1,7)=substr(s.ValuationDate,1,7) ORDER BY x.ValuationDate DESC LIMIT 1";
  string filter="s.Id IN ("+before+") OR s.Id IN ("+openingSql+") OR s.Id IN ("+closingSql+")";
  if(route=="monthly")filter+=" OR (s.ValuationDate>='"+start.ToString("yyyy-MM-dd")+"' AND s.ValuationDate<='"+end.ToString("yyyy-MM-dd")+"' AND s.Id IN ("+monthEnds+"))";
  var snapshots=Read("SELECT s.AccountId,a.FriendlyName,s.Id,s.ValuationDate,s.LiquidAssetsIls FROM AskLatestSnapshots s JOIN Accounts a ON a.Id=s.AccountId WHERE "+filter+" ORDER BY s.AccountId,s.ValuationDate");
  if(snapshots.Truncated)return Finish("History too broad","The requested scope exceeds the result budget. Specify a narrower year or portfolio so complete observations can be used.");
  var records=snapshots.Rows.Where(r=>r[4]!=null&&DateTime.TryParse(Convert.ToString(r[3]),out _)).Select(r=>new Observation(Convert.ToInt64(r[0]),Convert.ToString(r[1])!,Convert.ToInt64(r[2]),DateTime.Parse(Convert.ToString(r[3])!,CultureInfo.InvariantCulture),Convert.ToDecimal(r[4],CultureInfo.InvariantCulture))).ToList();
  if(records.Count==0)return Finish("No recorded values","No usable recorded portfolio values are available for this scope.");
  if(route=="monthly") {
   foreach(var col in new[]{"Portfolio","Month","Opening recorded date","Closing recorded date","Opening NIS","Closing NIS","Change NIS","Change %"})table.Columns.Add(col);
   foreach(var group in records.GroupBy(r=>r.AccountId)) {
    var ordered=group.OrderBy(r=>r.Date).ToList();var previous=ordered.LastOrDefault(r=>r.Date<start);
    foreach(var month in ordered.Where(r=>r.Date>=start&&r.Date<=end).GroupBy(r=>new{r.Date.Year,r.Date.Month}).OrderBy(g=>g.Key.Year).ThenBy(g=>g.Key.Month)) {
     var first=month.First();var last=month.Last();var opening=previous??first;if(previous!=null&&(last.Date.Year-previous.Date.Year)*12+last.Date.Month-previous.Date.Month>1)warnings.Add("Observation gap before "+last.Date.ToString("yyyy-MM")+": this change spans the actual recorded dates, not a complete calendar month.");decimal? change=last.Date==opening.Date?null:last.Value-opening.Value;
     table.Rows.Add(last.AccountName,last.Date.ToString("yyyy-MM"),opening.Date.ToString("yyyy-MM-dd"),last.Date.ToString("yyyy-MM-dd"),opening.Value.ToString("N2"),last.Value.ToString("N2"),change?.ToString("N2")??"insufficient distinct observations",change.HasValue&&opening.Value!=0?(change.Value/opening.Value*100).ToString("N2"):"not available");previous=last;
    }
   }
   return Finish("Monthly recorded changes",Format(table),"Periods use the actual recorded dates shown. When no earlier baseline exists, the first observation is the opening value. Portfolios are reported separately to avoid summing unaligned dates.");
  }
  if(route=="valuation") {
   foreach(var col in new[]{"Portfolio","Requested date","Opening recorded date","Closing recorded date","Opening NIS","Closing NIS","Change NIS","Change %"})table.Columns.Add(col);
   var changeRequested=Has(question,@"change|gain|loss|lost|difference");
   foreach(var group in records.GroupBy(r=>r.AccountId)) {
    var ordered=group.OrderBy(r=>r.Date).ToList();var last=ordered.LastOrDefault(r=>r.Date<=(dates.Length==0?DateTime.MaxValue:end));if(last==null)continue;
    var first=changeRequested?(ordered.LastOrDefault(r=>r.Date<=start)??ordered.FirstOrDefault(r=>r.Date>=start&&r.Date<=last.Date)):last;if(first==null)continue;
    var change=last.Value-first.Value;table.Rows.Add(last.AccountName,dates.Length>0?end.ToString("yyyy-MM-dd"):"latest recorded",first.Date.ToString("yyyy-MM-dd"),last.Date.ToString("yyyy-MM-dd"),first.Value.ToString("N2"),last.Value.ToString("N2"),changeRequested?change.ToString("N2"):"not requested",changeRequested&&first.Value!=0?(change/first.Value*100).ToString("N2"):"not available");
   }
   return Finish("Recorded portfolio values",Format(table),"Dates are as-of observations, not interpolated values. All requested and actual dates are shown.");
  }
  var type=Has(question,@"\bstocks?\b")?"stock":Has(question,@"\bfunds?\b")?"fund":Has(question,@"\bbonds?\b")?"bond":null;
  string selectType=hasType?"h.SecurityType":"NULL AS SecurityType";
  var metricChange=route=="ranking"&&Has(question,@"change|gain|loss|gainers|losers");
  var dataSql="SELECT s.AccountId,h.Name,h.Symbol,h.ValueIls,"+selectType+",s.ValuationDate,s.LiquidAssetsIls FROM AskCurrentHoldings h JOIN AskCurrentSnapshot s ON s.Id=h.SnapshotId";
  var periodBounds=records.GroupBy(r=>r.AccountId).ToDictionary(g=>g.Key,g=>(First:g.Where(r=>r.Date<=start).OrderBy(r=>r.Date).LastOrDefault()??g.Where(r=>r.Date>=start&&r.Date<=end).OrderBy(r=>r.Date).FirstOrDefault(),Last:g.Where(r=>r.Date<=end).OrderBy(r=>r.Date).LastOrDefault()));
  if(metricChange) {
   var periodIds=records.GroupBy(r=>r.AccountId).SelectMany(g=>new[]{g.Where(r=>r.Date<=start).OrderBy(r=>r.Date).LastOrDefault()??g.Where(r=>r.Date>=start&&r.Date<=end).OrderBy(r=>r.Date).FirstOrDefault(),g.Where(r=>r.Date<=end).OrderBy(r=>r.Date).LastOrDefault()}).Where(x=>x!=null).Select(x=>x!.Id).Distinct().ToArray();
   if(periodIds.Length==0)return Finish("No baseline","No usable snapshots are available for the requested period.");
   dataSql="SELECT s.AccountId,h.Name,h.Symbol,h.ValueIls,"+selectType+",s.ValuationDate,s.LiquidAssetsIls FROM AskLatestHoldings h JOIN AskLatestSnapshots s ON s.Id=h.SnapshotId WHERE s.Id IN ("+string.Join(",",periodIds)+")";
  }
  if(!metricChange&&(yearMatch.Success||dates.Length>0||Has(question,"last year")))dataSql="SELECT s.AccountId,h.Name,h.Symbol,h.ValueIls,"+selectType+",s.ValuationDate,s.LiquidAssetsIls FROM AskLatestHoldings h JOIN AskLatestSnapshots s ON s.Id=h.SnapshotId WHERE s.Id IN (SELECT x.Id FROM AskLatestSnapshots x WHERE x.AccountId=s.AccountId AND x.ValuationDate<='"+end.ToString("yyyy-MM-dd")+"' ORDER BY x.ValuationDate DESC LIMIT 1)";
  var holdings=Read(dataSql);if(holdings.Truncated)return Finish("Holdings result incomplete","Please narrow the scope; there are too many holdings for a complete local comparison.");
  var known=holdings.Rows.Where(r=>r[3]!=null&&(type==null||SecurityClassification.Classify(Convert.ToString(r[4]))==type)).ToList();
  if(type!=null)warnings.Add("Classification filter uses recognized explicit SecurityType labels only. Missing/unknown types are excluded; profile: "+profile);
  foreach(var col in new[]{"Portfolio ID","Security","ID / ticker","Type","Opening / recorded date","Closing date","Opening / value NIS","Closing NIS","Change NIS","Weight %"})table.Columns.Add(col);
  var rows=new List<(long Account,string Name,string Symbol,string Type,DateTime First,DateTime Last,decimal Start,decimal End,decimal? Weight)>();
  foreach(var group in known.GroupBy(r=>(Account:Convert.ToInt64(r[0]),Key:Convert.ToString(r[2]) is {Length:>0} key?key:Convert.ToString(r[1])!))) {
   var values=group.OrderBy(r=>Convert.ToString(r[5])).ToList();var first=values.First();var last=values.Last();var opening=Convert.ToDecimal(first[3]);var closing=Convert.ToDecimal(last[3]);
   var firstDate=DateTime.Parse(Convert.ToString(first[5])!);var lastDate=DateTime.Parse(Convert.ToString(last[5])!);
   if(metricChange&&periodBounds.TryGetValue(group.Key.Account,out var bounds)&&bounds.First!=null&&bounds.Last!=null){opening=firstDate==bounds.First.Date?opening:0;closing=lastDate==bounds.Last.Date?closing:0;firstDate=bounds.First.Date;lastDate=bounds.Last.Date;}
   rows.Add((group.Key.Account,Convert.ToString(last[1])!,Convert.ToString(last[2])!,Convert.ToString(last[4])??"not recorded",firstDate,lastDate,opening,closing,last[6]!=null&&Convert.ToDecimal(last[6])!=0?closing/Convert.ToDecimal(last[6])*100:null));
  }
  var countMatch=Regex.Match(question,@"\b(?:top|largest|biggest|best|worst)\s+(\d{1,3})\b",RegexOptions.IgnoreCase);var limit=countMatch.Success?Math.Clamp(int.Parse(countMatch.Groups[1].Value),1,100):route=="allocation"?500:5;
  var sorted=Has(question,@"worst|losers|smallest")?rows.OrderBy(r=>metricChange?r.End-r.Start:r.End):rows.OrderByDescending(r=>metricChange?r.End-r.Start:r.End);
  foreach(var r in sorted.Take(limit))table.Rows.Add(r.Account,r.Name,r.Symbol,r.Type,r.First.ToString("yyyy-MM-dd"),r.Last.ToString("yyyy-MM-dd"),r.Start.ToString("N2"),r.End.ToString("N2"),metricChange?(r.End-r.Start).ToString("N2"):"not requested",r.Weight?.ToString("N2")??"not available");
  return Finish(metricChange?"Recorded holding changes":"Current holdings / allocation",Format(table),metricChange?"Opening and closing records are used; this calculation does not adjust for trading flows.":"Weights use each holding's own current snapshot total. No unknown security type is inferred.");
 }
 static string Format(DataTable table)=>table.Rows.Count==0?"No matching records were found.":string.Join("\t",table.Columns.Cast<DataColumn>().Select(c=>c.ColumnName))+"\n"+string.Join("\n",table.Rows.Cast<DataRow>().Select(r=>string.Join("\t",r.ItemArray)));
 private sealed record Observation(long AccountId,string AccountName,long Id,DateTime Date,decimal Value);
}


