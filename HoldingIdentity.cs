using System.Text;
namespace AskCons;
public static class HoldingIdentity
{
    private static readonly HashSet<string> SummaryLabels = new(StringComparer.OrdinalIgnoreCase) {
        "סהכ", "סךהכל", "total", "grandtotal", "portfoliototal", "totalportfolio",
        "totalportfoliovalue", "accounttotal", "totalaccountvalue"
    };
    public static bool IsSummaryLabel(string? name, string? symbol = null) {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var key = new string(name.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit).ToArray());
        if (key.Equals("total", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(symbol)) return false;
        return SummaryLabels.Contains(key);
    }
}

