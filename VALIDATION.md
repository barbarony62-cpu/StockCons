# AskCons V0.7.0 validation — 25 October 2026

- Combined release includes English response policy/repair, named-company comparison grounding/relevance checks and optional public research.
- 123 deterministic synthetic regression checks pass, including source byte preservation, read-only SQL enforcement, scoped/full-database copying, financial arithmetic/estimates, conversation repair, comparison relevance, English repair, canonical public-only request construction, unsafe-link rejection, XML external-entity rejection and compiled WPF layout/rendering.
- Live integration successfully fetched Google News RSS excerpts for Tesla and Apple, then produced an English comparison through local Qwen3 4B with recorded local evidence and a public request/source audit. That integration run passed 125 checks (122 deterministic checks plus three live checks). Review identified a subset-versus-entire-portfolio inference; explicit totals/position counts and a new deterministic regression were added afterward.
- Source read-only contract and WPF XML/resource checks passed.
- Self-contained Windows x64 publish is provided. GitHub workflow builds this single combined version and its installer.

No real financial database was opened or changed during these tests. Synthetic financial values were sent only to localhost Ollama; public requests contained fixed public company searches only. OLLAMA_HOST and OLLAMA_NO_CLOUD were not changed.

Limitations: research is based on public news search excerpts and source links, not full filings, verified real-time market prices or a fundamentals API. Research supports the documented 20-company directory and previews requests before sending. The local model can still misunderstand, overstate claims or reason incorrectly; passing integration checks does not validate an investment conclusion. Review Analysis details, underlying sources, dates and assumptions. Further model-quality evaluation and expanded public coverage remain future work.
