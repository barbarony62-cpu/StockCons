# AskCons V0.8.2 — reliability, full-screen reading and copying

This single release includes all V0.8.1 features and the fixes below. Extract the portable ZIP and run AskCons.exe with local Ollama running. Keep the existing Ollama environment variables unchanged.

## Full-screen reading
Use Answer — full screen above the current result and Conversation — full screen above the transcript. Each opens its own maximized, resizable reader with selectable text, scrolling, Copy all and Close / Esc. Readers update when the underlying content changes. Analysis details now uses the same full-screen reader.

For a table, the reader presents the complete table as tab-separated text with headers. Chart answers retain their textual calculation evidence; use Copy image to share the visible chart.

## Copying and sharing
- Select answer/question text and press Ctrl+C, or use the native right-click menu. Read-only text remains selectable.
- Tables support selected-cell/row copying with headers. Right-click a table for Copy full table; exported text includes all returned rows, not just the visible ones.
- Right-click labels, security names/IDs, status text and other non-editable controls for Copy this text.
- Copy screen exports current answer/table, conversation, conversation questions, portfolio security list, status/timer and draft as text. It can include local private portfolio data: you control where you paste it.
- Copy image places a screenshot of the visible app content on the Windows clipboard, including charts and graphics.
- Full-screen readers have Copy all. Diagnostics are selectable and copyable too. Copying does not modify portfolio records or send anything over the network.

## Local-first calculations
With AI analyst enabled, recognized portfolio calculations run directly against the read-only local database copy before web research or model inference:
- Month-by-month recorded portfolio changes, with actual opening/closing dates and percentage changes. Graph requests reuse these computed figures.
- Current and dated portfolio valuations and value changes, using actual as-of observations.
- Current/datable holdings allocation and rankings by position value or recorded value change when that metric is explicit.
- First-owned dates when a security resolves unambiguously to a recorded identifier.

Monthly calculations use the newest capture per valuation date, prior recorded month-end baseline where available, and the actual recorded dates. Missing months are not filled with zero. Gaps and partial first periods are disclosed. All-account monthly calculations are reported separately, avoiding sums of unaligned dates. Recorded value change is not performance adjusted for deposits, withdrawals, trading, dividends or FX.

Explicit ISO dates and common English month/day dates are supported by the direct path. Unrecognized or more complex requests retain the flexible analyst. Estimates, interpolation, extrapolation and scenarios remain available with warnings. The direct routes are shortcuts for reliable common calculations, not restrictions on the analyst's broader capabilities.

SecurityType is inspected and logged when available. Stock/fund/bond filters use recognized explicit labels; blank or unknown types are excluded with a warning rather than inferred. Inspect Analysis details for the observed profile. This release does not claim every imported classification label is understood.

## Web relevance and privacy
Web research remains optional and has no confirmation popup. It is skipped for a completed local calculation, even if enabled. For other requests, research runs when the question indicates external context such as news, business outlook, valuation or an investment comparison. The public-securities field does not force a search for an unrelated local calculation.

Search-result relevance now checks the public security name/ID before following a result. Israeli numeric IDs or distinctive names with fund qualifiers prevent unrelated Sigma chemical/government results from becoming evidence about a Sigma fund. If no relevant source passes, the analyst must disclose the gap.

The previous fixed-company restriction remains removed. Public identities can come from portfolio clicks, explicit public identifiers and supported simple question parsing. Use Public securities for ambiguous/outside-portfolio names, tickers, ISINs or Israeli IDs, separated by semicolons. Never put private account data in that public-only field.

English and Hebrew search results, accessible public HTML and compatible structured market responses are supported; explanations remain English. Public providers receive public identities/topics, not holdings quantities, costs, account names, portfolio values, SQL or full chat. Source pages are untrusted data. Availability and identity resolution remain imperfect; paywalls, PDFs, JavaScript-only pages, delayed quotes and currency/exchange mapping may require further adapters.

## Diagnostics and failures
Analysis details records the original question, scope, route, web decision, actual SQL/results, row counts, classification profile, timings, model plans when produced and failure stage. It is available from request start and remains available after a failure. If the model fails before generating a query, the diagnostic stage identifies that fact rather than suggesting the database has no data.

Completed deterministic results are returned without waiting for an AI explanation. Flexible analysis retains bounded tools, streamed focused comparisons and preserved-evidence fallback. Local model quality/hardware still affect questions needing model interpretation; public sources and model conclusions require review.

## Existing features and protection
Accordion conversations, saved questions/answers, current securities sidebar inserting both name and ID, elapsed timer, all-account scope, charts and estimates remain. Local history is stored under %LOCALAPPDATA%/AskCons. Original financial source: %LOCALAPPDATA%/Consolidation/consolidation.db.

The source is opened read-only. Generated queries run only inside the scoped, indexed, query-only RAM copy with SQLite authorizer and resource limits. Copy/result limits are disclosed. Your OLLAMA_HOST=127.0.0.1:11434 and OLLAMA_NO_CLOUD=1 remain unchanged.

## Build and verification
Windows and .NET 8 SDK:

    dotnet run --project tests/AskCons.Tests.csproj -c Release
    python tools/verify_readonly.py
    python tools/verify_window_resources.py
    dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

The full ZIP includes the GitHub workflow and installer script for this one combined version. See VALIDATION.md for exact checks and limits.
