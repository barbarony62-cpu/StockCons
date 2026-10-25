# AskCons V0.7.0 — open-ended local portfolio analyst

## Run
Extract the portable ZIP, close the older AskCons, and run AskCons.exe.
Start your existing Ollama installation with qwen3:4b available.
Choose an account (or **All accounts / full database** for cross-account analysis) and leave **AI analyst** checked (the default).

The default chat is no longer restricted to the classic operation catalog. It can inspect the available schema, read relevant records, compose SQL calculations, use arithmetic tools, infer values with explicit assumptions, interpolate, extrapolate, and discuss the results.

## Public research and English answers
All analyst explanations are instructed to use English, including for Hebrew questions. Hebrew model answers are retried in English. Named multi-company comparisons get fresh relevant evidence and reject unrelated holding results and unrequested first-owned lookups.

To research Tesla and Apple, check **Web research**, then ask "Which is better, Tesla or Apple? Compare risks and fit within my portfolio." A preview shows the exact public URLs before any network request. Cancel keeps analysis local. Leave the checkbox off for local-only analysis.

This release uses Google News RSS search excerpts, with English locale and a fixed recent-earnings/valuation/risks topic. Sources show titles, reported publication dates (or missing dates), retrieval time and HTTPS links. They are search excerpts, not full-page or filing verification; there is no live quote or fundamentals API. The answer must qualify missing evidence. Analysis details includes the outgoing-request audit. URLs in the text can be copied into your browser.

A deliberately finite public company directory supports TSLA, AAPL, MSFT, GOOGL, AMZN, NVDA, META, BRK.B, AMD, INTC, NFLX, JPM, V, MA, XOM, TSM, AVGO, ORCL, WMT and COST. Name the company explicitly in each researched question, including follow-ups. Unsupported companies produce a local answer with a disclosed research limitation. The model cannot send arbitrary queries, conversation history or SQL results to the web. Public requests are rebuilt exclusively from canonical directory entries and a constant topic. Up to four companies are researched per question.

Portfolio data and source SQL stay local. Web providers receive ordinary connection information plus the public company searches, which disclose research interests. No browser cookies or default credentials are used; redirects are disabled. Retrieved content has no network or write tool. Your existing OLLAMA_HOST=127.0.0.1:11434 and OLLAMA_NO_CLOUD=1 should remain unchanged. Windows firewall/proxy restrictions may separately block research; failures are disclosed and local analysis continues.

These controls do not guarantee the local model's conclusions. Check relevance, dates, citations, assumptions and private calculations before relying on an answer.

## Try it
- What changed in my portfolio this year? Investigate the largest contributors.
- Interpolate my portfolio value between the available observations on a date I specify.
- Project the account a year ahead using the recent trend, then compare a 5% annual growth scenario.
- Estimate cash as a residual, explain the assumption, and show the calculation.
- How concentrated am I in the largest holdings? Compare with last year.
- Graph my account value by month.
- When did I first buy Tesla? Show the recorded ownership history.
- Now change the assumption / exclude Tesla / use a different period / explain that result.

Requests are interpreted by the local model; their success depends on the available records and the model's reasoning. A missing baseline cannot become a recorded fact. The analyst can supply an explicit scenario or a useful partial answer rather than refusing the entire question.

## Estimates are allowed
The application displays compulsory warnings for completed interpolation/extrapolation/scenario tools and for SQL results classified as inferred, interpolated, extrapolated or scenario-based. Warnings are appended by application code, even if the model omits them from its explanation.

Built-in methods:
- Linear interpolation between the nearest observations.
- Linear extrapolation from the nearest two observations.
- Least-squares linear regression.
- Constant compound growth from observed points or an explicit annual-rate scenario, using a 365.25-day year.
- One-point constant carry-forward, clearly marked as an assumption when a trend cannot be established.

The built-in estimation tools omit missing observations rather than replacing them with zero. Conflicting values for the same date may be averaged for an estimate with an ambiguity warning; preferred helper views avoid mixing multiple captures in the first place. These estimates are not recorded valuations or guaranteed forecasts.

Custom calculations and estimates can also be expressed through read-only SQL. Numeric projection and percentage requests are steered through calculation tools before the final explanation.

## Inspect the evidence
Click **Analysis details** to inspect the copied schema, read queries, tool results, formulas, estimates, assumptions and any failed/repaired tool attempts.

Computed query results and scalar calculations appear alongside the AI explanation. AI-generated SQL can still choose the wrong rows, metric or assumptions, and prose can be wrong. The warnings and audit trail make that distinction visible; they do not establish universal accuracy.

First owned retains its recorded meaning and is not proof of the actual purchase transaction date. Relevant ownership markers are retrieved independently of generated SQL and supplied as grounding.

## Source protection and account scope
Generated SQL never executes against consolidation.db.

1. The original database is opened with SQLite ReadOnly mode and query_only enabled.
2. Account-linked rows are copied into an isolated in-memory database in a consistent read transaction.
3. The in-memory database becomes query-only.
4. A SQLite authorizer permits reads and approved calculation functions and rejects mutations, schema changes, attachments, pragmas and extension loading.

For an individual account, only account-linked rows are copied; unrelated tables are listed as excluded. Choosing **All accounts / full database** explicitly includes all non-system database tables and allows cross-account calculations and global reference data. Scope is never expanded by a model-generated query. Combined-account helper views preserve captures separately for each account; comparisons must use aligned dates or disclose differences.

Sorting/query temporary storage is also held in memory. No migrations, bank operations or changes to financial records are performed.

## Data coverage and helper views
All account-linked tables that fit the memory budget are discoverable by the analyst, including stored ownership events, quantities, annual records, net-worth observations and other compatible tables.

Convenience views:
- AskLatestSnapshots: newest capture for each account and valuation date.
- AskCurrentSnapshot: latest valuation date per account.
- AskLatestHoldings / AskCurrentHoldings: corresponding holdings with recognized summary labels excluded.
- AskFirstOwned: explicit First owned records.

Raw copied tables remain available for broader investigations. The is_summary(Name,Symbol) function is available to SQL. No reliable security-type classification is manufactured; the AI may infer one if it labels the assumptions.

## Limits and privacy
- Uses the existing local qwen3:4b runtime over HTTP loopback; no cloud inference endpoint is used. The separate optional public-research component uses HTTPS.
- Original portfolio records are not altered. Existing saved question history still lives in %LOCALAPPDATA%/AskCons/questions.json.
- Conversation/tool evidence and the current analysis details are held in memory for this app session.
- Source path remains %LOCALAPPDATA%/Consolidation/consolidation.db.
- Up to 128 MB of raw copied values or 3 million copied cells; incomplete copies are reported.
- Each SQL result is limited to 500 rows / bounded text; aggregates can still scan the complete copied data. Truncated results are identified.
- Queries have an approximately eight-second execution budget; the model can repair or narrow them.
- A message can perform multiple tool calls within a bounded loop. Cancel stops pending work; the overall request timeout is ten minutes.
- Empty/invalid inputs or mathematically undefined results may require an assumption or another method. Completed parts can still be returned.
- These are resource and source-protection boundaries, not a fixed list of permitted financial questions.

Uncheck AI analyst to access the earlier bounded-query interface if desired.

## Build and verify
Windows and .NET 8 SDK:

    dotnet run --project tests/AskCons.Tests.csproj -c Release
    python tools/verify_readonly.py
    python tools/verify_window_resources.py
    dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

Optional local-model tests use synthetic records:

    $env:ASKCONS_LIVE_ANALYST="1"
    dotnet run --project tests/AskCons.Tests.csproj -c Release

GitHub Actions runs the regression suite before building the V0.7.0 installer. Upload the complete source contents, including .github, tests and tools.

