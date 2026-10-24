# Validation — AskCons V0.4.0

Validated on Windows on 24 October 2026.

- Release compilation succeeded.
- 29 synthetic regression checks passed. They cover security ambiguity, exact ticker matching, account scoping, snapshot ordering, missing historical data, aligned share valuations, sorting, unsupported/contradictory filters, percentage changes, presence versus zero, conversation context, ordinal references, planner message separation, clarification, cancellation, loopback restrictions and unchanged database bytes.
- Compiled WPF window constructed and laid out at 1280 x 900; a rendered preview was inspected.
- Existing read-only and WPF resource source checks passed.
- Three optional live Qwen3 4B smoke checks passed using only synthetic context: a two-item recorded value-change ranking, a monthly graph of the second item retaining the period, and clarification of a profit question.
- Earlier live runs exposed malformed plans, lost ordinal context, missing result limits and incorrect presentation. These led to operation-specific schema constraints, required parameters and explicit reference context.
- Self-contained win-x64 portable application published successfully.

The live checks are a narrow smoke test, not a broad accuracy benchmark. No real portfolio was used for tests. Production database compatibility has not been exercised against your actual records, and the Inno Setup installer has not been run locally. The included GitHub workflow builds that installer after the regression checks.

Current limits: snapshot value analysis; no transaction-adjusted investment returns, classification, market causality or multi-security charts. Conversations remain in memory for this run. Inspect actual snapshot dates on financial answers.

