using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AskCons;
public static class ResearchSelection {
 public static PublicCompany PublicIdentity(string value) {
  value=value.Trim();
  if(value.Length is <1 or >120 || !Regex.IsMatch(value,@"^[\p{L}\p{N}\s.'’&()_:+-]+$") || Regex.IsMatch(value,@"\b(account|password|balance|quantity|paid|portfolio|shares owned)\b",RegexOptions.IgnoreCase))throw new ArgumentException("Public securities accepts security names, tickers, ISINs or Israeli security IDs only, separated by semicolons.");
  var known=PublicResearch.Companies.FirstOrDefault(c=>c.Symbol.Equals(value,StringComparison.OrdinalIgnoreCase)||c.Name.Equals(value,StringComparison.OrdinalIgnoreCase)||c.Aliases.Any(a=>a.Equals(value,StringComparison.OrdinalIgnoreCase))); if(known!=null)return known;
  var identified=Regex.Match(value,@"^(.+?)\s+([A-Za-z0-9][A-Za-z0-9._:+-]*[0-9][A-Za-z0-9._:+-]*|[A-Za-z0-9]+\.[A-Za-z]{1,4})$");
  if(identified.Success)return new(identified.Groups[2].Value,identified.Groups[1].Value,[]);
  return new(value.Contains(' ')?"":value,value,[]);
 }
 public static List<PublicCompany> Resolve(string question,IEnumerable<PortfolioSecurity> holdings,string explicitPublic="") {
  var result=new List<PublicCompany>();
  if(!string.IsNullOrWhiteSpace(explicitPublic))result.AddRange(explicitPublic.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(PublicIdentity));
  foreach(var h in holdings) {
   bool match=(!string.IsNullOrWhiteSpace(h.Id)&&Regex.IsMatch(question,@"(?<![\p{L}\p{N}])"+Regex.Escape(h.Id)+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase)) || question.Contains(h.Name,StringComparison.OrdinalIgnoreCase);
   if(match)result.Add(new(h.Id,h.Name,[]));
  }
  foreach(Match m in Regex.Matches(question,@"\[ID:\s*([\p{L}\p{N}._:+-]+)\]",RegexOptions.IgnoreCase))result.Add(PublicIdentity(m.Groups[1].Value));
  foreach(var c in PublicResearch.Resolve(question))if(!result.Any(r=>r.Symbol.Equals(c.Symbol,StringComparison.OrdinalIgnoreCase)))result.Add(c);
  if(result.Count==0 && string.IsNullOrWhiteSpace(explicitPublic)) {
    var one=Regex.Match(question,@"^\s*(?:research|analyze|analyse|investigate|tell me about)\s+([\p{L}][\p{L}\p{N} .&'_-]{0,90}?)[?.!]*\s*$",RegexOptions.IgnoreCase);
    if(one.Success&&!Regex.IsMatch(one.Groups[1].Value,@"\b(my|our|your|paid|own|hold|shares|account|balance|quantity|portfolio)\b",RegexOptions.IgnoreCase))try{result.Add(PublicIdentity(one.Groups[1].Value));}catch(ArgumentException){}
  }
  if(result.Count<2) {
    var comparison=Regex.Match(question,@"^\s*(?:which\s+is\s+better[,:]?|what(?:'s| is)\s+better[,:]?|compare)\s+(?<a>[\p{L}][\p{L}\p{N} .&'_-]{0,70}?)\s+(?:or|versus|vs\.?|and)\s+(?<b>[\p{L}][\p{L}\p{N} .&'_-]{0,70}?)[?.!]*\s*$",RegexOptions.IgnoreCase);
    if(comparison.Success)foreach(var group in new[]{"a","b"}) {
       var value=comparison.Groups[group].Value.Trim();
       if(!Regex.IsMatch(value,@"\b(my|our|your|paid|own|hold|shares|account|balance|quantity|portfolio)\b",RegexOptions.IgnoreCase)) {
         try{var c=PublicIdentity(value);if(!result.Any(r=>r.Symbol.Equals(c.Symbol,StringComparison.OrdinalIgnoreCase)&&r.Name.Equals(c.Name,StringComparison.OrdinalIgnoreCase)))result.Add(c);}catch(ArgumentException){}
       }
    }
  }
  return result.GroupBy(c=>string.IsNullOrWhiteSpace(c.Symbol)?c.Name:c.Symbol,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
 }
}
public sealed class PublicWeb {
 public static bool PublicAddress(IPAddress ip) {
  if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();if(IPAddress.IsLoopback(ip)||ip.Equals(IPAddress.Any)||ip.Equals(IPAddress.IPv6Any))return false;
  if(ip.AddressFamily==AddressFamily.InterNetwork){var b=ip.GetAddressBytes();return b[0]!=0&&b[0]!=10&&b[0]!=127&&b[0]<224&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&b[1]==168)&&!(b[0]==100&&b[1]>=64&&b[1]<=127);}
  var v=ip.GetAddressBytes();return !ip.IsIPv6LinkLocal&&!ip.IsIPv6SiteLocal&&!ip.IsIPv6Multicast&&(v[0]&0xfe)!=0xfc;
 }
 public static async Task<string> Get(Uri uri,CancellationToken token) {
  if(uri.Scheme!="https"||uri.UserInfo!=""||uri.Port!=443)throw new ArgumentException("Only public HTTPS pages are allowed.");
  using var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false,Credentials=null,
    ConnectCallback=async(context,ct)=>{
      var ips=await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,ct);if(ips.Length==0||ips.Any(ip=>!PublicAddress(ip)))throw new HttpRequestException("Non-public destination blocked.");
      Exception? last=null;foreach(var ip in ips){var socket=new Socket(ip.AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(new IPEndPoint(ip,context.DnsEndPoint.Port),ct);return new NetworkStream(socket,true);}catch(Exception ex){last=ex;socket.Dispose();}}
      throw new HttpRequestException("Public host connection failed.",last);
    }};
  using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};client.DefaultRequestHeaders.UserAgent.ParseAdd("AskCons/0.8 PublicResearch");
  using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
  var media=response.Content.Headers.ContentType?.MediaType??"";
  if(!media.Contains("text")&&!media.Contains("xml")&&!media.Contains("json"))throw new InvalidOperationException("Non-text document skipped (PDF extraction is not available).");
  using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();var bytes=new byte[8192];int n;
  while((n=await stream.ReadAsync(bytes,token))>0){if(buffer.Length+n>1500000)throw new InvalidOperationException("Public page exceeds size limit.");buffer.Write(bytes,0,n);}
  var charset=response.Content.Headers.ContentType?.CharSet?.Trim('"');Encoding encoding=Encoding.UTF8;try{if(charset!=null)encoding=Encoding.GetEncoding(charset);}catch{}
  return encoding.GetString(buffer.ToArray());
 }
 public static string Extract(string html) {
  html=Regex.Replace(html,@"<(script|style|nav|footer|noscript)\b[^>]*>.*?</\1>"," ",RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromSeconds(1));
  html=Regex.Replace(html,@"</?(?:tr|p|div|h[1-6]|li|br|table)\b[^>]*>","\n",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1));
  html=WebUtility.HtmlDecode(Regex.Replace(html,"<[^>]*>"," ",RegexOptions.None,TimeSpan.FromSeconds(1)));
  var lines=html.Split('\n').Select(x=>Regex.Replace(x,@"\s+"," ").Trim()).Where(x=>x.Length>10).ToList();
  // Preserve order and table headings/units; select relevant financial passages, not a numeric interpretation.
  var relevant=lines.Where(x=>Regex.IsMatch(x,"price|currency|date|yield|earnings|revenue|profit|ratio|fund|bond|asset|expense|dividend|valuation|מחיר|תשואה|דמי ניהול|קרן|רווח|שווי|תאריך|אג.ח",RegexOptions.IgnoreCase)).Take(35);
  var text=string.Join("\n",relevant);if(text.Length<100)text=string.Join("\n",lines.Take(25));return text.Length>5000?text[..5000]+" [excerpt truncated]":text;
 }
}
public sealed class OpenResearch {
 public static List<Uri> SearchUris(PublicCompany company) {
  // Build only from public identity fields. Never take the full user question or model-generated query.
  var identity=(company.Name+" "+company.Symbol).Trim();if(identity.Length>250)throw new ArgumentException("Public identity too long.");
  return [new("https://news.google.com/rss/search?hl=en-US&gl=US&ceid=US%3Aen&q="+Uri.EscapeDataString(identity+" financial earnings fund bond valuation")),
   new("https://news.google.com/rss/search?hl=he&gl=IL&ceid=IL%3Ahe&q="+Uri.EscapeDataString(identity+" מניה קרן אגח נתונים כספיים תשואה")),
   new("https://www.bing.com/search?format=rss&setlang=en-US&q="+Uri.EscapeDataString(identity+" financial data investor relations price fund bond")),
   new("https://www.bing.com/search?format=rss&setlang=he-IL&q="+Uri.EscapeDataString(identity+" נתונים פיננסיים אתר הבורסה מאיה גלובס ביזפורטל פאנדר"))];
 }
 public async Task<ResearchReport> Fetch(IReadOnlyList<PublicCompany> companies,CancellationToken token,Action<string>? progress=null) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(55));var ct=deadline.Token;
  var evidence=new List<string>();var audit=new List<string>();var errors=new List<string>();int id=0;
  var selected=companies.Take(6).ToList();if(companies.Count>6)errors.Add("PARTIAL RESEARCH: first six explicitly selected securities researched; ask about the remainder separately.");
  var tasks=selected.SelectMany(c=>SearchUris(c).Select(u=>ReadSearch(c,u,ct))).ToList();
  var results=await Task.WhenAll(tasks);
  foreach(var result in results){audit.Add(result.Audit);if(result.Error!=null)errors.Add(result.Error);foreach(var source in result.Sources.Take(2))evidence.Add($"[W{++id}] {result.Company.Name} ({result.Company.Symbol}) | {source.Title}\nSource/search-reported date (not independently verified): {(source.Published==""?"not supplied":source.Published)}; retrieved {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n{source.Url}\nSearch excerpt: {source.Summary}");}
  var pages=results.Where(r=>r.Uri.Host=="www.bing.com").SelectMany(r=>r.Sources.Select(s=>(r.Company,Source:s))).DistinctBy(x=>x.Source.Url).Take(5).ToList();
  var readPages=await Task.WhenAll(pages.Select(async page=>{
   try{var uri=new Uri(page.Source.Url);var body=await PublicWeb.Get(uri,ct);return (Url:uri.AbsoluteUri,Text:PublicWeb.Extract(body),Error:"");}
   catch(Exception ex) when(!token.IsCancellationRequested){return (Url:page.Source.Url,Text:"",Error:ex is OperationCanceledException?"Research time budget reached":ex.Message);}
  }));
  foreach(var page in readPages){audit.Add("PUBLIC PAGE GET "+page.Url);if(page.Error!="")errors.Add(page.Url+": "+page.Error);else evidence.Add($"[W{++id}] PUBLIC PAGE EXCERPT | retrieved {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n{page.Url}\nPublication date: not independently established; retain dates/units in text. This is an extracted excerpt, not complete verification.\n{page.Text}");}
  // Quote endpoint is optional; an unsupported ticker/Israeli numeric ID falls back to the web sources above.
  foreach(var c in selected.Where(c=>Regex.IsMatch(c.Symbol,@"^[A-Za-z][A-Za-z0-9.^=-]{0,18}$")).Take(3)) {
   var uri=new Uri("https://query1.finance.yahoo.com/v8/finance/chart/"+Uri.EscapeDataString(c.Symbol)+"?interval=1d&range=1mo");audit.Add("PUBLIC MARKET GET "+uri.AbsoluteUri);
   try{var raw=await PublicWeb.Get(uri,ct);using var json=JsonDocument.Parse(raw);var root=json.RootElement.GetProperty("chart").GetProperty("result")[0];var meta=root.GetProperty("meta");var fields=new[]{"symbol","shortName","longName","instrumentType","currency","exchangeName","fullExchangeName","regularMarketPrice","regularMarketTime","previousClose","chartPreviousClose","fiftyTwoWeekHigh","fiftyTwoWeekLow"};var market=fields.Where(k=>meta.TryGetProperty(k,out _)).ToDictionary(k=>k,k=>meta.GetProperty(k).Clone());evidence.Add($"[W{++id}] STRUCTURED MARKET RESPONSE {c.Symbol} | retrieved {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n{uri}\n"+JsonSerializer.Serialize(market)+"\nPrices may be delayed; retain currency and regularMarketTime (Unix seconds). Do not mix quote units with NIS holdings.");}catch(Exception ex) when(!token.IsCancellationRequested){errors.Add("Market data "+c.Symbol+": "+(ex is OperationCanceledException?"time budget reached":ex.Message));}
  }
  token.ThrowIfCancellationRequested();progress?.Invoke("Public collection finished; preparing local analysis...");
  var limitations="Collection limitations: public pages can be incomplete, delayed, blocked or stale; excerpts are not full filings. Hebrew sources must be explained in English. Missing or inconsistent currencies/dates/units must not be silently reconciled. No successful quote response means no verified market price.\n"+string.Join("\n",errors);
  return new(string.Join("\n\n",evidence)+"\n\n"+limitations,string.Join("\n",audit));
 }
 private sealed record SearchResult(PublicCompany Company,Uri Uri,List<PublicSource> Sources,string Audit,string? Error);
 private static async Task<SearchResult> ReadSearch(PublicCompany c,Uri uri,CancellationToken ct) {
  try{return new(c,uri,PublicResearch.Parse(await PublicWeb.Get(uri,ct)),"PUBLIC SEARCH GET "+uri.AbsoluteUri,null);}
  catch(Exception ex){return new(c,uri,new(),"PUBLIC SEARCH GET "+uri.AbsoluteUri,"Search unavailable: "+(ex is OperationCanceledException?"time budget reached":ex.Message));}
 }
}

