using System.Text.RegularExpressions;
namespace AskCons;
// These checks constrain the answer family; they never substitute one financial calculation for another.
public static class QuestionContract
{
    public static string? RequiredOperation(string question,ConversationSession session) {
        if(Regex.IsMatch(question,@"\bfirst\s+(?:buy|bought|purchase|purchased|own|owned|acquire|acquired|held)\b|\b(?:start|started|begin|began)\s+(?:owning|holding)\b|\bfirst[- ](?:owned|ownership|purchase)\s+date\b|מתי.*(?:קניתי|רכשתי|החזקתי).*לראשונה",RegexOptions.IgnoreCase))
            return "holding_first_owned";
        if(Regex.IsMatch(question,@"\bquantity\s+history\b|\bhistory\s+of\s+(?:my\s+)?quantit|היסטוריית\s+כמויות",RegexOptions.IgnoreCase))
            return "holding_quantity_history";
        if(Regex.IsMatch(question,@"\b(?:holding|ownership)\s+history\b|היסטוריית\s+אחזקה",RegexOptions.IgnoreCase))
            return "holding_history";
        if(OwnershipQueries.IsOwnership(session.LastPlan?.operation??"") &&
           Regex.IsMatch(question,@"^\s*(?:and\b|what about\b|same for\b|how about\b|ומה לגבי)",RegexOptions.IgnoreCase))
            return session.LastPlan!.operation;
        if(session.PendingQuestion!=null && question.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length<=6 &&
           !Regex.IsMatch(question,@"\b(show|value|account|portfolio|rank|why|when|how|profit|return)\b",RegexOptions.IgnoreCase)) {
            var pending=RequiredOperation(session.PendingQuestion,new ConversationSession());
            if(pending!=null) return pending;
        }
        return null;
    }
    public static bool AllowsHistoryDates(string question,ConversationSession session) {
        bool Dated(string text)=>Regex.IsMatch(text,@"\b(?:19|20)\d{2}\b|\b(since|between|during|before|after|last|this|past|from|january|february|march|april|may|june|july|august|september|october|november|december|today|yesterday|month|year|week|day)\b|מאז|שנה|חודש|שבוע",RegexOptions.IgnoreCase);
        return Dated(question) ||
            session.PendingQuestion!=null && Dated(session.PendingQuestion) && RequiredOperation(question,session)!=null && RequiredOperation(session.PendingQuestion,new ConversationSession())==RequiredOperation(question,session) ||
            OwnershipQueries.IsOwnership(session.LastPlan?.operation??"") &&
            Regex.IsMatch(question,@"^\s*(and|what about|same for|how about)\b",RegexOptions.IgnoreCase) &&
            (session.LastPlan!.start_date!=null || session.LastPlan.end_date!=null);
    }
    public static object PlannerContext(string question,ConversationSession session) {
        var operation=RequiredOperation(question,session);
        if(operation==null) return session.PlannerContext();
        // New ownership requests must not inherit a ranking's dates, filters or operation.
        return new {selected_account=session.SelectedAccount,required_operation=operation,
            resolved_reference=session.ResolveReference(question),
            previous_security=OwnershipQueries.IsOwnership(session.LastPlan?.operation??"")?session.LastPlan?.security:null,
            previous_history_start=Regex.IsMatch(question,@"^\s*(and|what about|same for|how about)\b",RegexOptions.IgnoreCase) && OwnershipQueries.IsOwnership(session.LastPlan?.operation??"")?session.LastPlan?.start_date:null,
            previous_history_end=Regex.IsMatch(question,@"^\s*(and|what about|same for|how about)\b",RegexOptions.IgnoreCase) && OwnershipQueries.IsOwnership(session.LastPlan?.operation??"")?session.LastPlan?.end_date:null,
            pending_question=session.PendingQuestion!=null && RequiredOperation(session.PendingQuestion,new ConversationSession())==operation?session.PendingQuestion:null};
    }
    public static void Validate(string question,ConversationSession session,QueryIntent plan) {
        var required=RequiredOperation(question,session);
        if(required!=null && plan.action=="explain" && !Regex.IsMatch(question,@"\b(explain|why|source)\b|הסבר",RegexOptions.IgnoreCase))
            throw new ArgumentException("An ownership question requires a new history query, not a repetition of the previous answer.");
        if(plan.action=="execute" && OwnershipQueries.IsOwnership(plan.operation) && !AllowsHistoryDates(question,session) &&
           (plan.start_date!=null || plan.end_date!=null))
            throw new ArgumentException("An ownership-history date restriction was not requested. No history was silently restricted to this year.");
        if(plan.action=="execute" && required!=null && plan.operation!=required)
            throw new ArgumentException("The proposed query does not answer your ownership-history question. No unrelated financial query was executed. Please name the holding and ask again.");
    }
}



