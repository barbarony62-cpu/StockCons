using System.Text.Json;
namespace AskCons;
public static class QuerySchema
{
 public static JsonElement Build() {
  object Text()=>new {type="string"};
  object Choice(params string[] values)=>new {type="string",@enum=values};
  Dictionary<string,object> Common(string[] operations) => new() {
   ["action"]=Choice("execute"),["operation"]=Choice(operations),["start_date"]=Text(),["end_date"]=Text()
  };
  object Branch(Dictionary<string,object> properties,params string[] required)=>new {type="object",properties,required,additionalProperties=false};
  var account=Common(new[]{"account_value","account_change"});
  var holding=Common(new[]{"holding_value","holding_change","holding_share"});
  holding["security"]=Text();
  var accountSeries=Common(new[]{"account_series"}); accountSeries["interval"]=Choice("day","week","month");
  var holdingSeries=Common(new[]{"holding_series"}); holdingSeries["security"]=Text(); holdingSeries["interval"]=Choice("day","week","month");
  var rankings=Common(new[]{"top_holding_changes","bottom_holding_changes","holdings_list","holdings_filter","holdings_disappeared"});
  rankings["limit"]=new {type="integer",minimum=1,maximum=100};
  foreach(var name in new[]{"first_recorded_start","present_at_start","absent_at_end","require_present_at_end"}) rankings[name]=new {type="boolean"};
  foreach(var name in new[]{"minimum_change_nis","maximum_change_nis","minimum_start_value_nis","maximum_start_value_nis","minimum_end_value_nis","maximum_end_value_nis"}) rankings[name]=new {type="number"};
  rankings["name_contains"]=Text(); rankings["direction"]=Choice("gain","loss","all");
  rankings["sort_by"]=Choice("change","start_value","end_value","name"); rankings["sort_direction"]=Choice("asc","desc");
  foreach(var p in new[]{account,holding,accountSeries,holdingSeries,rankings}) p["presentation"]=Choice("auto","graph","table","text");
  var other=new Dictionary<string,object> {
    ["action"]=Choice("clarify","explain","unsupported"),["operation"]=Choice("unsupported"),
    ["clarification"]=Text(),["unsupported_reason"]=Text()
  };
  return JsonSerializer.SerializeToElement(new {anyOf=new[] {
   Branch(account,"action","operation","start_date","end_date"),
   Branch(holding,"action","operation","security","start_date","end_date"),
   Branch(accountSeries,"action","operation","start_date","end_date","interval","presentation"),
   Branch(holdingSeries,"action","operation","security","start_date","end_date","interval","presentation"),
   Branch(rankings,"action","operation","start_date","end_date","limit"),
   Branch(other,"action","operation")
  }});
 }
}


