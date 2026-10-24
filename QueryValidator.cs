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
        if (plan.security_type is not (null or "all"))
            throw new ArgumentException("Security classification is unavailable; ask for all holdings.");
        if (!In(plan.metric, "value", "change", "share") || !In(plan.entity, "account", "holding", "holdings"))
            throw new ArgumentException("Unknown metric or entity.");
        if (plan.operation.StartsWith("account_") && !string.IsNullOrWhiteSpace(plan.security))
            throw new ArgumentException("A security restriction cannot be applied to an account query.");
        bool ranking = plan.operation is "top_holding_changes" or "bottom_holding_changes" or "holdings_filter" or "holdings_list" or "holdings_disappeared";
        if (ranking && !string.IsNullOrWhiteSpace(plan.security))
            throw new ArgumentException("Use name_contains to filter a holdings list.");
        if (!ranking && (plan.present_at_start == true || plan.absent_at_end == true || plan.require_present_at_end == true ||
            plan.minimum_change_nis != null || plan.maximum_change_nis != null || plan.minimum_start_value_nis != null ||
            plan.maximum_start_value_nis != null || plan.minimum_end_value_nis != null || plan.maximum_end_value_nis != null ||
            plan.name_contains != null || plan.direction is "gain" or "loss" || plan.sort_by != null || plan.sort_direction != null || plan.limit != null))
            throw new ArgumentException("These filters require a holdings list or ranking; no restriction was ignored.");
        void Range(double? min, double? max) {
            if (min.HasValue && !double.IsFinite(min.Value) || max.HasValue && !double.IsFinite(max.Value) || min > max)
                throw new ArgumentException("Invalid numeric range.");
        }
        if (plan.absent_at_end == true && plan.require_present_at_end == true) throw new ArgumentException("A holding cannot be both absent and present at the end.");
        Range(plan.minimum_change_nis, plan.maximum_change_nis);
        Range(plan.minimum_start_value_nis, plan.maximum_start_value_nis);
        Range(plan.minimum_end_value_nis, plan.maximum_end_value_nis);

        plan.end_date ??= DateTime.Today.ToString("yyyy-MM-dd");
        plan.start_date ??= new DateTime(DateTime.ParseExact(plan.end_date, "yyyy-MM-dd", CultureInfo.InvariantCulture).Year, 1, 1).ToString("yyyy-MM-dd");
        var start = DateTime.ParseExact(plan.start_date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateTime.ParseExact(plan.end_date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (start > end || end > DateTime.Today)
            throw new ArgumentException("Choose a valid period ending no later than today.");
        return plan;
    }
    private static bool In(string? value, params string[] allowed) => value == null || Array.IndexOf(allowed, value) >= 0;
    private static bool ValidDate(string? value) => value == null || DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}



