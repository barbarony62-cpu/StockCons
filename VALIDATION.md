# AskCons V0.6.0 validation — 25 October 2026

- Release build and self-contained Windows x64 publish succeeded.
- 113 synthetic regression checks cover read-only enforcement, account isolation, explicit full-database scope, SQL joins/window functions, arithmetic, estimation, warnings, conversation transitions and WPF construction/rendering.
- Source read-only contract and WPF resource/XML verification passed.
- Live local Qwen3 4B checks produced a correctly labelled linear projection and a 5% compound scenario. A separate live topic-change check returned the stored first-owned date after prior scenario context. The latest combined live run completed its first projection; the remaining live sequence was not completed.
- Synthetic calculations verified a 1,000 to 2,000 history projected linearly to 2,341.9118, and a 93-day 5% annual compound scenario of 2,025.0009.
- Malicious SQL checks include updates, deletes, schema changes, attachments, extensions and stacked write statements. Source fixture bytes remain unchanged.
- The source database is opened read-only. AI-generated SQL runs against a query-only temporary RAM copy with SQLite authorizer and resource limits.

No real financial database was opened or changed during validation. Live checks used synthetic data. Model interpretation can still be wrong; inspect Analysis details for source queries, calculations and assumptions. Forecasts are assumptions, not guaranteed market outcomes. Copy/query limits are disclosed when reached.
