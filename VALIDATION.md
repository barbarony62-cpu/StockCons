# AskCons V0.5.0 validation — 25 October 2026

## Checks performed
- Release build and self-contained Windows x64 publish succeeded.
- 66 synthetic regression checks passed.
- Five optional live checks against the installed local Qwen3 4B passed: value-change ranking, ordinal graph follow-up, profit clarification, ranking-to-first-owned transition and all-time holding-history request. These used synthetic context only.
- First-owned tests use explicit stored events and check the source label, account scope, absence of a year-to-date window, history-only security resolution and no fallback to quantity observations.
- Conversation checks reproduce a prior holdings-disappeared ranking followed by "when did I first buy tesla?". Wrong operations, stale filters and unrequested history date windows are rejected.
- Missing-table compatibility is tested without migrations.
- Holdings and quantity-history reads leave the synthetic database's bytes unchanged.
- The compiled WPF history table was laid out at 1280 x 900, rendered and visually inspected. Long event descriptions and date ranges wrap.
- Existing source read-only and WPF resource checks passed.

## Scope
The adapter was checked against the supplied ConsolidationNG V1.57.2 source schema. No real financial database was opened or changed. The recorded date shown by your screenshot is not hard-coded in the application; the application reads the selected account's stored events.

A First owned event does not prove the actual transaction date. Missing events remain missing. Snapshot quantities are labelled observations.

The natural-language contract covers recognized question families and is not a guarantee that every wording is interpreted correctly. Broader annual-history, net-worth and cached-ranking capabilities are mapped in CAPABILITIES.md but not enabled in this version.

GitHub Actions builds the installer after the regression runner. The Inno Setup installer has not been run locally; a separately published portable Windows application is supplied.

