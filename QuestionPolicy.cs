using System.Text.RegularExpressions;
namespace AskCons;
public static class QuestionPolicy
{
    public const string MetricQuestion = "By best/worst, do you mean recorded NIS value change, current holding value, or investment return? I can calculate the first two; transaction-adjusted investment returns are not available.";
    public static bool NeedsMetricClarification(string question) =>
        Regex.IsMatch(question, @"\b(best|worst)\s+(?:(?:\d+|one|two|three|four|five|ten)\s+)?(?:holdings?|stocks?|securities|investments?|performers?)\b|הכי\s+(?:טוב|גרוע)", RegexOptions.IgnoreCase)
        && !Regex.IsMatch(question, @"recorded\s+(?:NIS\s+)?value\s+change|change\s+in\s+(?:recorded\s+)?value|שינוי\s+(?:בשווי|בערך)", RegexOptions.IgnoreCase);
}
