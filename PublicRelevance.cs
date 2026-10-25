using System.Text.RegularExpressions;
namespace AskCons;
public static class PublicRelevance {
 public static bool Matches(PublicCompany company,PublicSource source) {
  string Normalize(string text)=>Regex.Replace(text.ToLowerInvariant(),@"[^\p{L}\p{N}]","");
  var content=source.Title+" "+source.Summary+" "+source.Url;
  var symbol=company.Symbol.Trim();
  if(symbol.Length>=2&&Regex.IsMatch(content,@"(?<![\p{L}\p{N}])"+Regex.Escape(symbol)+@"(?![\p{L}\p{N}])",RegexOptions.IgnoreCase))return true;
  var words=Regex.Matches(company.Name,@"[\p{L}\p{N}]+").Select(m=>m.Value).Where(w=>w.Length>=3&&!Stop.Contains(w)).ToArray();
  if(words.Length==0)return false;
  bool names=words.All(w=>Normalize(content).Contains(Normalize(w)));
  var qualifiers=Regex.Matches(company.Name,@"\d+").Select(m=>m.Value).ToArray();
  // Numeric Israeli IDs require the exact ID or the distinctive name AND numeric fund qualifiers.
  return names&&qualifiers.All(n=>Regex.IsMatch(content,@"(?<!\d)"+Regex.Escape(n)+@"(?!\d)"));
 }
 static readonly HashSet<string> Stop=new(StringComparer.OrdinalIgnoreCase){"inc","ltd","corp","corporation","class","the","fund","etf","קרן","נאמנות"};
}
