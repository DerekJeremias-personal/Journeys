<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
- 2026-09-19T19:34:42Z — Four contracts only (Q1); hydrate collect is a C1 appendix owned by U1 so it is not a fifth HTTP surface. Matches Q2 shared-schema plus Q3 ownership without adding a public route.

## Deviations
- 2026-09-19T19:34:42Z — Did not write a full rewrite of EventPayloadResponseDto or tenant event-model JSON. C1 pins the existing POST path and fail-the-event order; payload remains tenant JsonElement.

## Tradeoffs
- 2026-09-19T19:34:42Z — OpenAPI for outbound webhook body, shared-schema for in-process hops and MCP. No AsyncAPI or new REST between units (Q2). Existing RestApiConfig.Retry stays; no second Award retry loop (Q5).

## Open questions
- 2026-09-19T19:34:42Z — Host/appsettings key name for non-fatal webhook throw is deferred to Functional Design / Code Generation; behavior is already decided.
