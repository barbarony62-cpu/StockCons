using System.IO;
using System.Text.Json;
namespace AskCons;
public sealed class SavedMessage { public string Role {get;set;}=""; public string Text {get;set;}=""; }
public sealed class SavedConversation {
 public string Id {get;set;}=Guid.NewGuid().ToString("N"); public long AccountId {get;set;}
 public string Title {get;set;}="New conversation"; public DateTime Updated {get;set;}=DateTime.UtcNow;
 public List<SavedMessage> Messages {get;set;}=new();
}
public sealed class ConversationArchive {
 public List<SavedConversation> Conversations {get;set;}=new();
 public static ConversationArchive Load(string path) {try{return File.Exists(path)?JsonSerializer.Deserialize<ConversationArchive>(File.ReadAllText(path))??new():new();}catch{return new();}}
 public void Save(string path) {Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
 public SavedConversation Create(long accountId) {var c=new SavedConversation{AccountId=accountId};Conversations.Insert(0,c);return c;}
}
public sealed record PortfolioSecurity(long AccountId,string AccountName,string Name,string Id) {
 public string Display=>Name+"\nID / ticker: "+(string.IsNullOrWhiteSpace(Id)?"not recorded":Id)+" · "+AccountName;
 public string Insertion=>Name+(string.IsNullOrWhiteSpace(Id)?" [ID not recorded]":" [ID: "+Id+"]");
}
public static class QuestionInsertion {
 public static (string Text,int Caret) Insert(string text,int start,int length,PortfolioSecurity security) {
  start=Math.Clamp(start,0,text.Length);length=Math.Clamp(length,0,text.Length-start);
  var value=(start>0&&!char.IsWhiteSpace(text[start-1])?" ":"")+security.Insertion+" ";
  return (text.Remove(start,length).Insert(start,value),start+value.Length);
 }
}
public sealed partial class ReadOnlyPortfolio {
 public List<PortfolioSecurity> CurrentSecurities(long accountId) {
  using var c=Connect();using var cmd=c.CreateCommand();cmd.CommandText="""
  SELECT DISTINCT s.AccountId,a.FriendlyName,h.Name,COALESCE(h.Symbol,'') FROM Holdings h
  JOIN Snapshots s ON s.Id=h.SnapshotId JOIN Accounts a ON a.Id=s.AccountId
  WHERE ($a=-1 OR s.AccountId=$a) AND s.Id=(SELECT x.Id FROM Snapshots x WHERE x.AccountId=s.AccountId ORDER BY x.ValuationDate DESC,x.CapturedAt DESC,x.Id DESC LIMIT 1)
  AND NOT askcons_summary(h.Name,COALESCE(h.Symbol,'')) ORDER BY a.FriendlyName,h.Name
  """;cmd.Parameters.AddWithValue("$a",accountId);using var rd=cmd.ExecuteReader();var list=new List<PortfolioSecurity>();
  while(rd.Read())list.Add(new(rd.GetInt64(0),rd.GetString(1),rd.GetString(2),rd.GetString(3)));return list;
 }
}
