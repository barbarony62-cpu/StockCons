# AskCons V0.8.2 validation — 25 October 2026

- 162 deterministic synthetic checks passed, including all prior read-only, scope, financial arithmetic/estimate, streaming/fallback, conversation persistence and WPF checks.
- Local monthly-change regression uses duplicate same-day captures, a prior-year baseline, missing months, null newer totals and a second account. C# figures, newest-capture selection, gap notes, account isolation and unchanged source bytes pass without model or network access.
- The 12,000-snapshot / 96,000-holding fixture now checks a complete twelve-month direct calculation without raw-row truncation, AI or web. Existing indexed snapshot/current-holding performance tests and deliberate SQL budget cleanup remain passing.
- Direct current/as-of values (ISO and English dates), chart values, explicit SecurityType stock filtering and recorded first-owned meaning pass. Source byte hashes remain unchanged.
- Routing tests skip web for local monthly calculations and retain external research for company comparisons. Public relevance tests reject Sigma-Aldrich/government results for the Israeli Sigma fund and accept an exact matching security ID.
- UI checks confirm selectable read-only answer text, full table export with headers/underlying rows, a maximized reader retaining a 200-line exchange, and whole-screen text containing account/security/timer information. The populated main layout was rendered at 1280 x 900 and visually inspected.
- Read-only source contract and WPF XML/resources pass. Self-contained Windows x64 publish succeeds. No real portfolio database was opened in these checks.

Clipboard API calls are user-triggered. Tests inspect generated copy content rather than replacing the user's Windows clipboard. Public research availability and investment conclusions were not revalidated through live services for this release; the changes are covered with local/mocked evidence. A relevance filter can still reject useful ambiguous results or accept misleading pages; source review remains necessary.

Known boundaries: deterministic parsing covers the documented common requests/date formats, not every financial sentence. Complex/unsupported requests retain the local AI path. Classification uses recognized explicit labels only. Month gaps are reported, not fabricated. Full-screen tables use TSV text; charts can be shared as a visible screen image. Copying private app content to the clipboard is under user control.
