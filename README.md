# AskCons V0.3.2 — local financial query planning

Independent WPF application. The ConsolidationNG SQLite database is opened strictly in read-only mode. No schema changes, migrations or writes to the financial database.

## Changes since 0.2.0
- Extended the Qwen structured planner with composable conditions: beginning/ending value ranges, value changes, name filters, opening/ending presence, date intervals, sorting, ranking limits.
- Added a strict C# QueryValidator. Unknown operations and unimplemented aggregation/grouping are rejected rather than silently ignored.
- Removed heuristic question-pattern overrides for ranking and series selection; the structured planner is authoritative.
- Existing table/chart rendering, keyboard shortcuts, question history and original icon retained.
- Qwen runs locally via Ollama at 127.0.0.1:11434.

## Known limitations
This is a bounded composable query engine, **not unrestricted SQL generation**. Unimplemented operations are rejected. It does not classify securities as stock/fund/bond, explain market causality, compute transaction-adjusted returns, or combine multiple security lines on a single chart. Historical availability and security name matching depend on the existing DB. A 150-question regression suite is not yet implemented. Windows compilation and runtime testing are required.

## Build
`dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish`

GitHub Actions builds the installer automatically on push to main.

## 0.3.2 hotfix
- Normalize top/bottom portfolio ranking plans before validating single-security requirements.
- Fix stale title bar version.
- A stocks-only filter still requires verified classification; AskCons does not pretend funds are stocks.


## V0.3.2 ranking interpretation fix
A portfolio-wide top/bottom ranking does not require a named security. The planner's inferred `security_type=stock` is normalized to `all` for general wording (including "stocks existed"), with a clear result note that stocks, funds and bonds are included. Explicit type-only restrictions remain unsupported rather than being silently dropped.
