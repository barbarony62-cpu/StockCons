# AskCons V0.1.5

Complete independent .NET 8 WPF application. Build automatically on push to main via GitHub Actions.

## Changes
- Enter asks; Shift+Enter inserts a newline.
- Per-account local question history, clickable to re-ask.
- Removed technical database/model details from sidebar.
- Best/worst N holdings by **absolute NIS change in recorded holding values**, regardless of transactions. Defaults to year-to-date; missing holdings at a valid snapshot count as zero.
- Uses only read-only SQLite connections, never writes to ConsolidationNG.
- Ollama remains local.

**Interpretation caveat:** The rankings use all recorded holdings (including funds/bonds), not exclusively equities, because security classification varies. Values are not transaction-adjusted profits.

## Install
Commit the entire extracted project to `StockCons/main`. Download the installer artifact from GitHub Actions.
