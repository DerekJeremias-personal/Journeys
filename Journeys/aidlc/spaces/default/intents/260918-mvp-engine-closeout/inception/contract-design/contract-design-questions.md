# Contract Design Questions

Intent `mvp-engine-closeout`. Pin formal contracts so Construction can build the four library units in parallel. Do not invent capability ids, a new host, AWS/CDK, or `Journeys.UX`. Linear issues JOU-1–JOU-4 already exist; pull-back is on disk.

**Already decided (not re-asked):** in-process sync on ProcessEvent (Domain Design Q3); four `library` units in `Journeys.API`; expire-on-process depends on earn-date cascade; no `/points/expire`; closed webhook field list; throw fails ProcessEvent by default with a host switch for webhook; missing dest PAT stays in source.

**Sources:** `unit-of-work.md`, `unit-of-work-dependency.md`, `components.md`, `requirements.md`, Linear copy under `unit-linear-copy/`.

## Proposed plan (defaults if you accept the recommended options)

- **Four contracts:** existing ProcessEvent HTTP (no new routes), U3→U2 shared hop schema, outbound tenant `rest_api` webhook + `NotificationOutcomePayload`, MCP `NotificationConfigId` critical row.
- **Formats:** shared-schema YAML for in-process seams; OpenAPI for the webhook POST body; MCP as the published matrix row. No queues, no new AWS.
- **Owners:** U2 hop clock, U3 ProcessEvent order, U4 webhook + MCP + docs, U1 hydrate collect (shared-schema, not HTTP).
- **Versioning:** additive DTO/JSON; consumers ignore unknown fields; MCP `MatrixVersion` bump only.
- **Failure:** already-decided throw/skip rules; existing adapter timeouts; no new retry or idempotency store.

## Q1. Which boundaries get a formal spec this stage?

This decides the rows in `contract-summary.md`. Domain Design already kept in-process calls; this is which agreements we write down so units can land without rewriting each other.

A. Four specs — existing ProcessEvent HTTP (no new routes), expire→cascade hop schema, outbound tenant webhook + closed payload, MCP `NotificationConfigId` row (recommended)
B. Skip ProcessEvent HTTP — only hop schema, webhook, and MCP
C. Also treat `HydrateState` and `SaveLedgerExpirations` as public HTTP APIs
X. Other (please specify)

[Answer]: A. Four specs — existing ProcessEvent HTTP (no new routes), expire→cascade hop schema, outbound tenant webhook + closed payload, MCP NotificationConfigId row (2026-09-19, **Mode:** Guide me)

## Q2. How do we write those specs?

Units Generation said no new APIs, queues, or events between units. This picks the spec format so Construction does not invent a REST hop or a queue.

A. Shared-schema YAML for in-process seams (U3→U2 hop; U1 hydrate collect); OpenAPI for the webhook POST body; MCP as the published matrix row — no new queues or AWS (recommended)
B. AsyncAPI for the webhook (treat send as an event bus)
C. New REST between the four units
X. Other (please specify)

[Answer]: A. Shared-schema YAML for in-process seams (U3→U2 hop; U1 hydrate collect); OpenAPI for the webhook POST body; MCP as the published matrix row — no new queues or AWS (2026-09-19, **Mode:** Guide me)

## Q3. Who owns each spec?

A wrong owner means two units change the same shape. Delivery Planning will schedule Bolts against these owners.

A. U2 owns hop/clock schema; U3 owns ProcessEvent order (existing HTTP unchanged); U4 owns webhook payload + MCP row; U1 owns hydrate collect as shared-schema, not HTTP (recommended)
B. One owner for every spec (treat the API host as the only contract owner)
C. External webhook consumers own the payload shape
X. Other (please specify)

[Answer]: A. U2 owns hop/clock schema; U3 owns ProcessEvent order (existing HTTP unchanged); U4 owns webhook payload + MCP row; U1 owns hydrate collect as shared-schema, not HTTP (2026-09-19, **Mode:** Guide me)

## Q4. Versioning and breaking changes

Webhook consumers and MCP clients already exist. We should not invent `/v2` ProcessEvent this increment.

A. Additive DTO/JSON — consumers ignore unknown fields; MCP `MatrixVersion` bump only; no `/v2` ProcessEvent (recommended)
B. Version the webhook URL this increment
C. Semver Core assemblies as the contract version
X. Other (please specify)

[Answer]: A. Additive DTO/JSON — consumers ignore unknown fields; MCP MatrixVersion bump only; no /v2 ProcessEvent (2026-09-19, **Mode:** Guide me)

## Q5. Error, timeout, and retry at each boundary

Requirements already set fail-the-event vs skip. This pins what the contract table says about timeouts and retries so Construction does not add a queue or a second idempotency store.

A. Keep the decided fail/skip rules; use the existing `rest_api` adapter timeouts; no new retry loop or idempotency store; reprocess may POST again; host switch may make webhook throw non-fatal (recommended)
B. Add a circuit breaker and queue for webhook send
C. Retry the webhook three times inside Award before failing the event
X. Other (please specify)

[Answer]: A. Keep the decided fail/skip rules; use the existing rest_api adapter timeouts; no new retry loop or idempotency store; reprocess may POST again; host switch may make webhook throw non-fatal (2026-09-19, **Mode:** Guide me)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
