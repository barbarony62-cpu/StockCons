using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace AskCons;
public sealed record PublicCompany(string Symbol,string Name,string[] Aliases);
public sealed record PublicSource(string Title,string Url,string Published,string Summary);
public sealed record ResearchReport(string Evidence,string Audit);
public sealed class PublicResearch {
 public static readonly PublicCompany[] Companies=[
 new("TSLA","Tesla",["tesla","טסלה"]),new("AAPL","Apple",["apple","אפל"]),new("MSFT","Microsoft",["microsoft"]),new("GOOGL","Alphabet",["alphabet","google"]),new("AMZN","Amazon",["amazon"]),new("NVDA","NVIDIA",["nvidia"]),new("META","Meta Platforms",["meta","facebook"]),new("BRK.B","Berkshire Hathaway",["berkshire"]),new("AMD","Advanced Micro Devices",["amd"]),new("INTC","Intel",["intel"]),new("NFLX","Netflix",["netflix"]),new("JPM","JPMorgan Chase",["jpmorgan"]),new("V","Visa",["visa"]),new("MA","Mastercard",["mastercard"]),new("XOM","Exxon Mobil",["exxon"]),new("TSM","Taiwan Semiconductor",["tsmc"]),new("AVGO","Broadcom",["broadcom"]),new("ORCL","Oracle",["oracle"]),new("WMT","Walmart",["walmart"]),new("COST","Costco",["costco"])];
 public static List<PublicCompany> Resolve(string question)=>Companies.Where(c=>c.Aliases.Append(c.Symbol).Any(a=>Regex.IsMatch(question,@"(?<![\p{L}\p{N}])"+Regex.Escape(a)+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase))).ToList();
 public static List<PublicCompany> ParseSelection(string selection) {
  var tokens=selection.Split([',',';',' '],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
  if(tokens.Length is <1 or >4)throw new ArgumentException("Select one to four supported public tickers, separated by commas.");
  return tokens.Select(t=>Companies.FirstOrDefault(c=>c.Symbol.Equals(t,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("Unsupported public ticker: use the company list in the Web research tooltip. No request was sent.")).Distinct().ToList();
 }
 public static Uri RequestUri(PublicCompany company) {
  var canonical=Companies.SingleOrDefault(c=>c.Symbol==company.Symbol)??throw new ArgumentException("Unsupported company.");
  return new Uri("https://news.google.com/rss/search?hl=en-US&gl=US&ceid=US%3Aen&q="+Uri.EscapeDataString("\""+canonical.Name+"\" (earnings OR valuation OR risks) when:90d"));
 }
 public static List<PublicSource> Parse(string xml) {
  using var sr=new StringReader(xml);using var reader=XmlReader.Create(sr,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000});
  return XDocument.Load(reader).Descendants("item").Take(3).Select(x=>new PublicSource(Clean((string?)x.Element("title")),SafeUrl((string?)x.Element("link")),Clean((string?)x.Element("pubDate")),Clean((string?)x.Element("description")))).Where(s=>s.Url!="").ToList();
 }
 static string Clean(string? s) {var t=WebUtility.HtmlDecode(Regex.Replace(s??"","<[^>]*>"," "));return t.Length>700?t[..700]:t;}
 static string SafeUrl(string? s)=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme=="https"&&!u.IsLoopback&&u.UserInfo==""?u.AbsoluteUri:"";
 public async Task<ResearchReport> Fetch(IReadOnlyList<PublicCompany> companies,CancellationToken token) {
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false}){Timeout=TimeSpan.FromSeconds(25)};
  var evidence=new List<string>();var audit=new List<string>();int id=0;
  foreach(var company in companies) {
   var uri=RequestUri(company);audit.Add("PUBLIC GET "+uri.AbsoluteUri);
   using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
   using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();var bytes=new byte[8192];int n;
   while((n=await stream.ReadAsync(bytes,token))>0){if(buffer.Length+n>2000000)throw new InvalidOperationException("Public response exceeded size limit.");buffer.Write(bytes,0,n);}
   var sources=Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
   if(sources.Count==0)evidence.Add(company.Symbol+": No public search results returned.");
   foreach(var s in sources)evidence.Add($"[W{++id}] {company.Name} ({company.Symbol}) | {s.Title}\nPublished: {(s.Published==""?"not supplied":s.Published)}; retrieved {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n{s.Url}\nSEARCH EXCERPT (not full-page verification): {s.Summary}");
  }
  return new(string.Join("\n\n",evidence),string.Join("\n",audit));
 }
}

