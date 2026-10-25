# AskCons V0.8.1 validation — 25 October 2026

- 145 synthetic regression checks passed on the final source.
- Identified the problematic query shape: the in-memory copy lacked source indexes and nested current-snapshot views repeatedly scanned historical data. The fix creates RAM indexes and computes canonical/current snapshots once, before enforcing query-only access.
- A larger fixture contains 12,000 snapshots and 96,000 holdings across two accounts, including duplicate dates with capture order differing from ID order. Latest capture selection, current holdings comparison, whole-portfolio counts and all-account separation pass. Comparison/count queries complete within a five-second assertion, below the existing eight-second SQL execution budget.
- A deliberately expensive recursive query reaches the SQL budget; the interruption is translated to a clear timeout. A subsequent SELECT succeeds, checking cleanup of the progress handler and interrupt state.
- Large and small source fixture hashes remain unchanged after analysis. No original financial database was opened or modified.
- Existing 139 checks for read-only SQL enforcement, scoped copies, calculations, estimates, conversation archive, name-and-ID insertion, English response/fallback, streaming and compiled WPF construction/layout remain passing.
- Read-only source contract and WPF XML/resource checks passed. Self-contained Windows x64 publish succeeded.

This patch targets the database execution error, not investment reasoning or external-source availability. Earlier V0.8.0 live public-research/model tests are documented in the prior release; no additional real-portfolio or live-web test was needed for this internal query change. All V0.8.0 UI/research features and their documented limitations remain.

