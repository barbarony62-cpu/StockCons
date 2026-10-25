# AskCons V0.8.0 — portfolio chat and public research

## Run
Extract the entire portable ZIP, close the older AskCons and run AskCons.exe. Keep Ollama running with qwen3:4b. Leave AI analyst checked. Original database: %LOCALAPPDATA%/Consolidation/consolidation.db.

Keep OLLAMA_HOST=127.0.0.1:11434 and OLLAMA_NO_CLOUD=1 unchanged. Local inference and portfolio analysis stay on your computer. Optional web collection is a separate HTTPS component.

## Conversations, securities and timer
- Left: expandable conversations for the selected portfolio. Expand to see every saved question; both the overall list and expanded contents scroll. Open/resume restores the exchange. Clicking a question restores that conversation and copies the question into the draft; it does not automatically send it. New conversation starts a separate thread.
- Questions AND answers are stored locally in %LOCALAPPDATA%/AskCons/conversations.json. Old questions.json entries are imported into an explicitly labelled group; answers that were never saved cannot be reconstructed. Saved tool evidence is not reused as verified data after reopening; the analyst can query again.
- Right: current securities for the selected portfolio, with account labels and recorded ID/ticker. Filter by name or ID. Click to insert both name and ID at the cursor without erasing the draft. All-accounts scope lists each portfolio's current securities. Unrecorded IDs are labelled missing, never invented.
- Elapsed time starts on Enter or Ask, updates while working, and remains visible after completion, failure or cancellation. Stage text identifies public collection, local analysis or generated text.

## Public research: any identified security, English and Hebrew sources
Enable Web research; there is no confirmation popup. Private quantities, costs, account names, portfolio values, SQL results and full conversation are not sent to public providers.

Research identities come from locally matched portfolio names/IDs, common public aliases, narrowly parsed simple questions such as "Which is better Toyota or Honda?", explicit [ID: ...] insertions, or the separate Public securities field. That field accepts public names, tickers, ISINs or Israeli security numbers, separated by semicolons. Use it to disambiguate a security or research one outside your holdings. Do not put account details in this public-only field. Ambiguous names still need qualification; an unidentified or unsupported quote symbol is never proof the security does not exist.

The previous 20-company restriction is removed. A small alias dictionary remains only as a convenience, not an access restriction. Any identified public security can be searched. At most six securities are researched per question to bound collection time, with a partial-research notice for extras.

Collection searches English and Hebrew news and the public web, including financial sites, exchange/issuer material and Israeli financial sources when indexed. It also attempts direct HTML/text page extraction from public search results and a structured Yahoo market response for compatible quote symbols. Coverage is best-effort, not a promise that a specific free site or every security has available data. Results show source URLs, reported publication dates or missing dates, retrieval times, and failure/availability notes. HTML extraction preserves financial text and table headings; it is not full filing verification. PDFs, authentication, paywalls, CAPTCHA and JavaScript-only pages are not bypassed. A blocked or unavailable source is disclosed; other evidence is retained. Successful market responses retain currency, exchange and quote timestamp; prices may be delayed. Israeli numeric IDs need exchange-specific quote mapping, so web research can work even when Yahoo does not recognize the ID.

The model explains evidence in English, including when the question or sources are Hebrew. Original source titles/text can retain their language. Public data is combined with private portfolio records locally. Analysis details contains the exact outgoing URLs, source excerpts and local evidence. Websites receive public security interests and ordinary network metadata, not your portfolio records.

## Response time and fallbacks
Simple comparisons use a focused one-response path with local holdings, whole-portfolio context and compact public evidence. Model output is streamed as it arrives. Collection has a 55-second budget; comparison generation has a 120-second budget. Partial English text is retained when possible. If the model cannot produce a valid response, the app returns available database evidence and collected sources, labelled as an incomplete analysis instead of discarding everything.

Other analytical questions retain the read-only SQL/calculator/estimate tool loop with per-step and overall analysis budgets. The outer request budget is four minutes. Cancel stops work. Response speed depends on your local model/hardware and load; these changes cannot guarantee a good investment explanation on every request.

## Portfolio analysis and read-only protection
Ask for recorded values, ownership history, holdings changes, allocation, charts, arithmetic, interpolation, extrapolation or explicit scenarios. Recorded value changes are not automatically investment profit. First owned is not necessarily the transaction date.

The original SQLite database is opened read-only; generated SQL runs only in a scoped, query-only RAM copy with a native SQLite authorizer. Account scope is enforced before analysis. All accounts / full database is an explicit UI option. Copy limits (128 MB or 3 million cells), query/result limits and missing data are disclosed.

Estimates remain permitted with application-appended warnings. Deterministic tools support linear interpolation/extrapolation, regression, compound scenarios and explicit one-point carry-forward assumptions. Inspect dates, currencies, units, assumptions and sources for important conclusions. Model interpretations, scraped figures and inferred classifications can be wrong.

## Build once
The full project and GitHub Actions workflow build this single combined V0.8.0 release with all changes.

    dotnet run --project tests/AskCons.Tests.csproj -c Release
    python tools/verify_readonly.py
    python tools/verify_window_resources.py
    dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

Optional synthetic live integration: set ASKCONS_LIVE_080=1 before running tests. It uses public research and local Ollama; do not enable it in normal offline CI. No real financial database is needed by the tests.
