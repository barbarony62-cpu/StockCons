# Consolidation → AskCons capability map (V0.5.0)

Audited against the supplied ConsolidationNG V1.57.2 source, not a live financial database.

| Information | Consolidation source | AskCons operation | Status and meaning |
|---|---|---|---|
| Account recorded value | Snapshots.LiquidAssetsIls | account_value, account_change, account_series | Supported; snapshot values, not transaction-adjusted return |
| Holding recorded values | Holdings.ValueIls + Snapshots | holding_value, holding_change, holding_series, holding_share | Supported; dated, account scoped |
| Holdings lists and rankings | Two complete snapshots | holdings_list/filter/disappeared, top/bottom_holding_changes | Supported; recognized summary rows excluded |
| Explicit first-owned event | SecurityHistoryEvents, EventType=First owned | holding_first_owned | New; earliest explicit event over all stored history, source retained |
| Stored ownership events | SecurityHistoryEvents | holding_history | New; event types and recorded quantities retained |
| Historical quantities | Holdings.Quantity + latest capture per valuation date | holding_quantity_history; also shown in holding_history | New; observations, not automatically transactions |
| Annual historical liquid assets | HistoricalAnnual via GetAnnualHistory | — | Mapped, not implemented in this release |
| Property, pension and net-worth observations | NetWorthObservations via GetNetWorthHistory | — | Mapped, not implemented; must not be confused with liquid portfolio value |
| Cached performance rankings | PerformanceRankingCache | — | Mapped, not used; underlying definitions require a separate audit |
| Transaction-adjusted profit, dividends, market causes | No suitable adapter calculation established | — | Unsupported |
| Bank capture/import/update, backups, migrations | Consolidation write services | — | Excluded from the read-only assistant |

Consolidation's GetSecurityHistory also derives labels from snapshot quantity changes, including a ratio-based corporate-action heuristic. AskCons does not recreate that heuristic as a fact: stored events retain their recorded labels, and snapshot quantities remain labelled observations.

## Question-to-answer contract
Recognized first-owned questions constrain the planner to holding_first_owned or a clarification/unsupported response. Recognized holding/quantity-history questions have their own corresponding constraints. An independent check rejects a mismatching executable operation before any financial query executes.

Fresh ownership requests receive a separate context without the previous ranking's operation, dates or filters. History-date fields are unavailable unless the question or a relevant follow-up requests a period. Date restrictions are also checked after generation.

These checks cover the recognized wording families in QuestionContract.cs; they are not a proof that every natural-language question is interpreted correctly.

