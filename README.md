# AskCons V0.5.0

## Run
Extract the portable Windows ZIP and run AskCons.exe with Ollama and qwen3:4b available.
The existing Consolidation database is opened read-only at:
%LOCALAPPDATA%\Consolidation\consolidation.db

## New in this version
- "When did I first buy Tesla?" reads the recorded First owned event, retaining that label rather than claiming a purchase transaction.
- "Show holding history for TSLA" displays stored events, quantities and source labels.
- "Show quantity history for TSLA" displays saved quantity observations.
- These queries cover all recorded history by default. Add an explicit period if desired.
- Archived securities can resolve from stored events even without valuation snapshots.
- Ownership questions cannot execute an unrelated holdings-disappeared ranking.
- Fresh history questions exclude stale ranking context. Clarification replies and short ownership follow-ups retain the relevant intent.
- History tables are chronological, and each row identifies its source.

See CAPABILITIES.md for the audited mapping of Consolidation's data to supported and currently unavailable AskCons operations.

## What dates mean
A stored First owned event is not proof of the actual purchase transaction. No first-owned date is inferred from the earliest snapshot or a quantity observation. If no explicit First owned event exists, AskCons says so. If multiple distinct dates are stored, the earliest is shown with a note.

Stored event dates, labels and sources are retained. Quantity differences are not automatically classified as purchases, sales or stock splits.

## Compatibility
Uses the SecurityHistoryEvents columns in the supplied ConsolidationNG V1.57.2 source, plus Holdings.Quantity for observed quantities. Missing history tables produce an explicit compatibility message; there are no schema changes or migrations. A database with no Quantity column returns no snapshot quantity observations.

Existing snapshot-query features and V0.4.1 summary-row exclusions remain. Broader net-worth, annual-history and cached-ranking sources are mapped but not implemented. Snapshot value changes remain distinct from investment profit and returns. Security identity still uses the existing symbol or exact recorded name.

Conversations remain in memory for the current run; the existing saved-question list remains local. The app uses HTTP loopback to Ollama; it does not change Ollama's network settings or Windows Firewall.

## Build and verify on Windows
Requires the .NET 8 SDK.

    dotnet run --project tests/AskCons.Tests.csproj -c Release
    python tools/verify_readonly.py
    python tools/verify_window_resources.py
    dotnet publish AskCons.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

Optional live checks against the local model, with synthetic context only:

    $env:ASKCONS_LIVE_OWNERSHIP="1"
    dotnet run --project tests/AskCons.Tests.csproj -c Release

The included GitHub Actions workflow runs regression checks and builds the V0.5.0 Windows installer. Include the .github, tests and tools folders when uploading the full project.

