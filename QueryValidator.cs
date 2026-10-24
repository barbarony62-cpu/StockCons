using System;
using System.Collections.Generic;
using System.Globalization;

namespace AskCons;

// The LLM is untrusted: only these validated dimensions reach the read-only data adapter.
public static class QueryValidator
{
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal) {
        "account_change", "account_value", "holding_change", "holding_value",
        "top_holding_changes", "bottom_holding_changes", "holdings_filter", "holdings_list",
        "holdings_disappeared", "holding_share", "holding_series", "account_series", "unsupported"
    };
    public static QueryIntent Validate(QueryIntent plan)
    {
        if (plan == null) throw new ArgumentException("AI returned no query plan.");
        if (!Operations.Contains(plan.operation)) throw new ArgumentException("AI requested an unknown financial operation.");
        if (plan.limit is < 1 or > 100) throw new ArgumentException("Requested result limit must be 1–100.");
        if (!ValidDate(plan.start_date) || !ValidDate(plan.end_date)) throw new ArgumentException("AI returned an invalid date.");
        if (!In(plan.interval, "day", "week", "month") || !In(plan.sort_by, "change", "end_value", "start_value", "name") ||
            !In(plan.direction, "gain", "loss", "all") || !In(plan.sort_direction, "asc", "desc") ||
            !In(plan.presentation, "auto", "table", "graph", "text") ||
            !In(plan.security_type, "all", "stock", "fund", "bond", "unknown"))
            throw new ArgumentException("AI returned an unsupported filter or display option.");
        if (plan.aggregation is not null && plan.aggregation is not ("none" or "sum" or "average" or "min" or "max"))
            throw new ArgumentException("Unknown aggregation requested.");
        if (plan.aggregation is "sum" or "average" or "min" or "max")
            throw new ArgumentException("The requested aggregation is not implemented in this adapter; no condition was ignored.");
        if (plan.group_by is not null && plan.group_by is not ("none" or "day" or "week" or "month"))
            throw new ArgumentException("Unknown grouping requested.");
        if (plan.group_by is "day" or "week" or "month" && plan.operation is not ("holding_series" or "account_series"))
            throw new ArgumentException("Grouping is supported for historical series only.");
        if (plan.name_contains?.Length > 150 || plan.security?.Length > 150)
            throw new ArgumentException("Security name or filter is too long.");
        if ((plan.operation is "holding_series" or "account_series") && (plan.group_by is "day" or "week" or "month"))
            plan.interval = plan.group_by;
        if ((plan.operation is "holding_change" or "holding_value" or "holding_series" or "holding_share") &&
            string.IsNullOrWhiteSpace(plan.security))
            throw new ArgumentException("The question needs a specific holding name or ticker.");
        return plan;
    }
    private static bool In(string? value, params string[] allowed) => value == null || Array.IndexOf(allowed, value) >= 0;
    private static bool ValidDate(string? value) => value == null || DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
