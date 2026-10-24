using System.Net.Http.Json;using System.Text.Json;
namespace AskCons;
public sealed class LocalInterpreter {
 private static readonly HttpClient Http=new(){BaseAddress=new Uri("http://127.0.0.1:11434/"),Timeout=TimeSpan.FromSeconds(120)};
 public async Task<QueryIntent> Interpret(string question,CancellationToken token){
 var today=DateTime.Today.ToString("yyyy-MM-dd");
 var prompt=$"""
 You are a STRICT parser for a local portfolio application. Today's local date is {today}.
 Convert the user's English or Hebrew financial question to a JSON object ONLY.
 Keys: operation (account_change, account_value, holding_change, holding_value, unsupported), security (string or null), start_date (YYYY-MM-DD or null), end_date (YYYY-MM-DD or null), language (en or he).
 'my Tesla investment' means my HOLDING, not market price. 'stock price performance' is unsupported because historical market prices may be absent.
 For account_change or holding_change set start_date and end_date. For account_value or holding_value set end_date.
 Interpret 'since January 1' as Jan 1 of the current year; 'six months' as six calendar months before today; 'today' as today's date.
 Do not invent financial numbers. Never return SQL. If ambiguous or not supported choose unsupported.
 Question: {question}
 """;
 using var response=await Http.PostAsJsonAsync("api/chat",new{model="qwen3:4b",stream=false,format="json",options=new{temperature=0,num_predict=220},messages=new[]{new{role="user",content=prompt}}},token);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));var output=doc.RootElement.GetProperty("message").GetProperty("content").GetString()??"{}";return JsonSerializer.Deserialize<QueryIntent>(output)??new QueryIntent();
 }
}
