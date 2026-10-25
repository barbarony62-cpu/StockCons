namespace AskCons;
public sealed class ConversationSession
{
 public string AnalysisEvidence {get;set;}="";
 public string SelectedAccount {get;set;}="";
 public QueryIntent? LastPlan {get;private set;}
 public List<string> OrderedSecurities {get;private set;} = new();
 public string LastAnswer {get;private set;} = "";
 public string? PendingQuestion {get;set;}
 public List<(string Role,string Text)> Turns {get;} = new();
 public void Commit(QueryIntent plan,string answer,IEnumerable<string> securities) {
 LastPlan=System.Text.Json.JsonSerializer.Deserialize<QueryIntent>(System.Text.Json.JsonSerializer.Serialize(plan));
 LastAnswer=answer; OrderedSecurities=securities.ToList(); PendingQuestion=null;
 }
 public string? ResolveReference(string question) {
  if(OrderedSecurities.Count==1 && System.Text.RegularExpressions.Regex.IsMatch(question,@"\b(it|that holding|that stock|this security)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return OrderedSecurities[0];
  var match=System.Text.RegularExpressions.Regex.Match(question, @"\b(first|second|third|fourth|fifth|1st|2nd|3rd|4th|5th)\s+(one|holding|security|stock|item)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  if(!match.Success) return null;
  var ordinal=match.Groups[1].Value.ToLowerInvariant();
  int index=ordinal switch {"first" or "1st"=>0,"second" or "2nd"=>1,"third" or "3rd"=>2,"fourth" or "4th"=>3,_=>4};
  return index<OrderedSecurities.Count?OrderedSecurities[index]:null;
 }
 public object PlannerContext()=>new { selected_account=SelectedAccount, previous_plan=LastPlan,ordered_securities=OrderedSecurities,pending_question=PendingQuestion,
 recent_messages=Turns.TakeLast(6).Select(t=>new {role=t.Role,text=t.Text.Length>500?t.Text[..500]:t.Text}) };
}
public sealed record SecurityCandidate(string Name,string Symbol) {
 public override string ToString()=>string.IsNullOrWhiteSpace(Symbol)?Name:$"{Name} ({Symbol})";
}




