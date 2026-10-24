# AskCons V0.4.0 — conversational local portfolio queries

## Run on Windows
Extract the portable ZIP into a new folder and run AskCons.exe.
Keep ConsolidationNG's existing database at %LOCALAPPDATA%\Consolidation\consolidation.db.
Start Ollama with qwen3:4b installed. Click Test AI connection.
No database migration or financial writes are performed.

## What's new
- In-memory conversations separated by selected account. Follow-ups include the previous validated plan, ordered security identifiers, recent turns and pending clarification.
- Clarify/execute/explain/unsupported actions; user messages are separated from system instructions.
- Chat transcript alongside the current answer/table/chart, New conversation, Cancel, three-minute request timeout.
- Account changes invalidate pending responses; closing the window cancels pending AI work.
- Security resolution scoped to the selected account, preferring exact names/tickers and asking when ambiguous.
- Exact instrument matching during calculation. Unknown/missing historical holdings do not become fabricated zero charts.
- Share calculations use the account and holding from one snapshot.
- Value-change answers explain that transactions and income are not adjusted.
- Removed keyword-based rewriting of financial questions and dates.
- Calculation service separated from WPF; unsupported/inapplicable filters rejected; corrected sorting.
- Holdings presence is distinct from a recorded zero valuation.
- AI endpoint restricted to HTTP loopback; default HTTP redirects disabled.
- JSON-schema-constrained model output; unknown JSON fields and invalid conversation actions rejected.

## Try a conversation
1. Show my five largest holdings at the end of September 2025.
2. And at the end of January 2025?
3. Show their recorded value changes from January 1 to September 30, 2025.
4. Graph the second one monthly.
5. Explain how you calculated that.

Follow-ups are interpreted by the local model and can still require correction. Context is supplied; this is not a guarantee that every paraphrase or multi-step request is supported. A series covers one account or one security. Comparing an arbitrary retained set of multiple securities is not yet implemented.

## Important behavior
- "Change" is recorded ending value minus opening value, not investment profit or return. Best/worst investment-performance requests should trigger clarification.
- Queries use the latest saved snapshot on or before each requested boundary, and show the actual dates. Old data may be used if there is no newer snapshot; inspect those dates.
- Rankings assume the source snapshots represent complete holdings; a missing instrument in a complete snapshot counts as absent. Null holding values fail the comparison.
- Series show saved observations only. Missing holdings or valuations are omitted rather than treated as zero. Lines connect available observations; no intermediate values are calculated.
- Current scalar value/change queries require matching saved valuations; they do not infer zero for a position absent from a boundary.
- The account total uses the existing LiquidAssetsIls field. Confirm that this is the total you intend to analyze.
- Security identity uses the database's existing ticker, or exact name where no ticker exists. There is no separate master security identifier or exchange disambiguation.
- Stock/fund/bond classification, transaction-adjusted profit, realized gains, dividends, market explanations, arbitrary aggregation and multi-security charts remain unsupported.
- Deterministic result labels are in English. Input/clarification quality in Hebrew depends on the local model.
- Explain repeats the last verified answer and describes the calculation; it does not invent economic causes.
- Conversations are kept only during this run. Existing per-account question history remains on disk at %LOCALAPPDATA%\AskCons\questions.json. New conversation clears current chat context, not saved question history.
- Nothing in this release changes Ollama's own cloud/network settings or your firewall. The app calls only its loopback endpoint.

## Build and test
Requires the .NET 8 SDK on Windows.

    dotnet run --project tests/AskCons.Tests.csproj -c Release
    python tools/verify_readonly.py
    python tools/verify_window_resources.py
    dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

Optional local model smoke test (uses synthetic context only):

    $env:ASKCONS_LIVE_MODEL="1"
    dotnet run --project tests/AskCons.Tests.csproj -c Release

The console regression runner uses a temporary synthetic SQLite database, checks read-only behavior with a file hash, exercises the planner through a fake HTTP handler, and constructs/layouts the compiled WPF controls. The optional live test checks a ranking, ordinal follow-up and profit ambiguity against the locally installed model; it is a smoke test, not an accuracy benchmark.

GitHub Actions runs the regression runner before publishing and builds the Inno Setup installer. Upload the entire source project including .github, tests and tools to the repository.


