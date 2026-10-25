# AskCons — requirements and V0.8.0 delivery

Delivered together:
- Always explain in English, including Hebrew questions; Hebrew/English research sources supported.
- Remove per-request public-search confirmation popup.
- Remove fixed 20-company access restriction. Resolve portfolio identities locally, common aliases and simple public-name questions; accept arbitrary public names/tickers/ISINs/Israeli IDs in the dedicated public-securities field.
- Search bilingual public financial information, extract accessible public page text, attempt structured market metadata, and disclose unavailable sources/units/dates.
- Keep private portfolio quantities, costs, account names, values, SQL and full chat away from public requests; retain local inference and read-only database analysis. Existing Ollama variables unchanged.
- Left accordion conversations: grouped saved questions and answers, expandable and scrollable, restored without automatically rerunning a question.
- Right scrollable/filterable securities for the selected portfolio; display and insert BOTH name and recorded ID/ticker at the question cursor without erasing text.
- Elapsed timer from Enter/Ask through completion/cancellation/failure.
- Focused comparisons, compact contexts, streaming generation and bounded-work fallback preserving completed evidence instead of blank timeout.

Remaining boundaries/future improvements:
- Dedicated provider adapters for inaccessible JavaScript/PDF financial reports and exchange-specific quote mappings, especially Israeli numeric identifiers.
- Stronger financial reasoning/source verification beyond the local model; no guarantee of a correct investment ranking.
- Optional downloadable company/security identifier catalogues for more automatic ambiguity resolution. Arbitrary explicit identities already work; ambiguous names may still need clarification.

See README.md and VALIDATION.md for actual implementation and checks.
