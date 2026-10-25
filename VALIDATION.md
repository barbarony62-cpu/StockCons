# AskCons V0.8.0 validation — 25 October 2026

## Completed checks
- 139 deterministic synthetic regression checks pass on the final source.
- New checks cover current portfolio securities/account isolation/summary exclusion, actual UI name-and-ID insertion preserving the draft, expandable conversation construction, grouped question/answer persistence, arbitrary English/Hebrew public identities beyond the prior alias list, natural comparisons outside that list, rejection of private question fragments as research identities, bilingual searches, financial HTML extraction and private/loopback/metadata destination blocking.
- Comparison checks cover the focused response path, local whole-portfolio context, English fallback preserving source evidence, and streamed text surviving an output-limit finish with an explicit partial-response label.
- Existing read-only SQL authorizer, immutable source-byte, account-scope, financial calculation, estimation and conversational repair tests remain passing.
- WPF resource/XML and read-only source-contract checks pass. The compiled UI was rendered at 1280 x 900 with a populated synthetic conversation accordion and securities sidebar and visually inspected.
- Live integration used English/Hebrew public searches, accessible public HTML extraction and successful structured market metadata retrieval. The first bounded-generation run preserved evidence rather than returning a blank timeout (~102 seconds). After streaming and context compaction, the live comparison returned English generated text and sources in ~96 seconds. It reached an output limit; final code now explicitly labels that condition and leads with the conclusion. These live tests preceded the final deterministic-only refinements; they do not establish investment accuracy.
- Self-contained Windows x64 release and the complete GitHub build project are provided together.

## Privacy and limits
No real financial database was opened during these tests. Synthetic portfolio values were used only for local model analysis. Public requests contained public security identity fields and fixed research topics, not private portfolio values or conversation history. Existing Ollama environment variables were not changed.

Collection has a 55-second budget, the focused comparison has a 120-second generation budget and retains partial evidence/text, and the outer UI request has a four-minute budget. Model speed depends on hardware/load; the change prevents silent loss of completed evidence rather than promising unlimited inference time or a correct investment conclusion.

Source availability varies. Public HTML extraction and optional quote metadata are not full filing verification; PDFs, paywalls, CAPTCHA and JavaScript-only sites are not bypassed. Quote symbols may require exchange mapping. Displayed source/search dates are not independently verified publication dates. The model can still misinterpret evidence. Inspect sources, units, currency, dates, calculations and assumptions.
