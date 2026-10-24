# AskCons V0.4.1 validation

The screenshot exposed a portfolio summary being treated as a security and a ranking whose order did not match value change. Synthetic fixtures now include Hebrew summary rows with differing punctuation, a genuine TotalEnergies holding, and a first-in-period snapshot after the requested January 1 boundary.

38 regression checks passed, including summary exclusion across all security query paths, unchanged account totals, ranking order, metric clarification for the exact screenshot question, first-recorded-day membership and unchanged database bytes. The WPF window is constructed and laid out by the regression runner.

Release Windows x64 compilation and self-contained publication were checked locally. The earlier V0.4.0 live-model smoke tests were not rerun for this hotfix. No real financial database was inspected or modified. The installer itself is built by the included GitHub workflow; the portable EXE is supplied separately.

Summary identification is limited to the explicit normalized labels in HoldingIdentity.cs. There is no authoritative row-type field in the currently supported adapter.
