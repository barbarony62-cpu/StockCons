# AskCons — needs and acceptance criteria

Recorded 25 October 2026. Original requirements with V0.7.0 implementation notes below; remaining scope is explicitly identified.

## Priority 1: Relevant conversation with English-only answers

Observed failure: the Hebrew question “מה עדיף? טסלה או אפל?” (Which is better, Tesla or Apple?) produced an unrelated Sigma 20/80 first-owned record, malformed Hebrew, and confused company/ticker associations.

Needs:
- Recognize comparison intent and resolve Tesla = TSLA and Apple = AAPL before querying. Ambiguity should lead to a focused clarification.
- Do not reuse irrelevant first-owned operations, securities or date filters from previous questions.
- Check that retrieved evidence matches the requested securities and question. Retry/repair an unrelated query; never present an unrelated fund as evidence about Tesla or Apple.
- Always respond in English, including when the user writes a question in Hebrew. The user will primarily work in English. Hebrew output and right-to-left answer layout are no longer requirements. Keep SQL/schema names out of normal prose unless needed to explain a limitation.
- Interpret an occasional Hebrew question when possible, but produce the explanation, clarification and warnings in English. Evaluate English comprehension, relevance and writing using real comparison and follow-up cases.
- For “better,” explain relevant criteria or ask about time horizon/risk preference; distinguish historical portfolio observations from investment outlook.
- Do not substitute a raw unrelated database dump for an answer. If relevant evidence is missing, explain precisely what is missing and what can still be compared.

Acceptance:
- Test "Which is better, Tesla or Apple?" and the Hebrew equivalent above, both in a new chat and after first-owned/history/scenario questions. Answers must be in English in every case.
- Both named securities resolve correctly; no unrelated security or first-owned answer appears.
- Human review finds the English coherent and readable; no Hebrew answer is generated even for a Hebrew question.
- Claims and calculations are supported by relevant evidence; uncertainty is explicit.

## Priority 2: Optional public web research with private local analysis

- Add local-only and public-research modes, plus optional outgoing-request preview.
- Public web requests contain only approved company identifiers and public research topics. Never send account names, quantities, purchase costs, portfolio values, database rows or full conversation history.
- Enforce this separation in code through a narrow research request contract, not just model instructions. Raw model-generated requests must not bypass the privacy boundary.
- Retrieve dated public evidence with source links, preferring company filings and earnings releases. Clearly distinguish public facts, private calculations and assumptions.
- Combine retrieved research with private portfolio evidence locally.
- Display an inspectable log of outgoing requests and retrieved sources. Disclose that company names reveal research interests.
- Treat web pages as untrusted data; retrieved text cannot authorize tools, database access or setting changes.
- Inspect the user's existing Windows network/environment restrictions before proposing specific changes. Preserve local inference restrictions and allow only the research component the required access. Do not broadly remove variables or protections.
- Missing network access or sources should produce a clear limitation with a useful local answer, rather than invented current facts.

Acceptance:
- A Tesla/Apple comparison can cite dated web evidence and separately discuss local portfolio concentration.
- Capture outbound requests in tests and verify that private fixture data never appears.
- Attempts to exfiltrate portfolio data through prompts, research queries, URLs or retrieved page instructions fail.
- Verify behavior with web access enabled, disabled and blocked by the current Windows configuration.


## V0.7.0 delivery status
Implemented together: English answer policy/repair; named comparison prefetch and relevance checks; fresh comparison context; optional previewed public searches; canonical identifier-only requests; source excerpts, dates and audit; unchanged Ollama variables; graceful local fallback; synthetic privacy and source-protection regressions.

Remaining: expand the finite 20-company public directory, full-page/filing verification, structured fundamentals/quotes, optional request-preview preference (this release always previews), and further model-quality evaluation. Public research currently uses search excerpts; it does not claim full-document verification. Model relevance/language checks reduce failures but are not semantic correctness guarantees.
