# Domain Design Questions

Intent `mvp-engine-closeout`. Brownfield closeout of four existing Core seams. Do not invent capability ids, a new host, or AWS/CDK. `Journeys.UX` is out. Refined Mockups was skipped (no operator UI).

**Sources:** `requirements.md`, `stories.md` (US1.1–US5.1), CodeKB `architecture.md` / `component-inventory.md`, affirmed `team-practices`.

## Proposed plan (defaults if you accept the recommended options)

- **No new components.** Catalogue the existing onion blocks this increment touches. Payload and `RulesEngineState.NotificationService` are additions *inside* existing blocks, not new building blocks.
- **Entities:** ownership + shape only (no types/cardinality — that is Functional Design).
- **AWS / UI:** out. Webhook transport stays the existing `rest_api` adapter.

## Q1. Run this stage or skip?

The stage runs when new logical building blocks are needed, and skips when the work only changes existing ones. This increment hydrates the journey tree, preserves earn-date on cascade, expires under the existing account lock, and fires the existing NotificationOutcome webhook.

A. Execute a thin catalogue of the existing blocks this increment touches — no new components (recommended; gives Units Generation named owners)
B. Skip — modifications to existing components only; Units Generation maps stories from the approved plan
X. Other (please specify)

[Answer]: A. Execute a thin catalogue of the existing blocks this increment touches — no new components (2026-09-19, **Mode:** Chat)

## Q2. If we catalogue, which blocks?

A. Existing Core/DTO/Notification only: EventService (process + lock), RulesService + JourneyNode (hydrate/collect), LoyaltyAccountService (bring-current + SaveLedgerExpirations), NotificationOutcome + `NotificationOutcomePayload` + existing `INotificationService` / `rest_api` adapter (recommended)
B. Also split a new Notification component out of Journeys.Notification
C. Also treat ProcessEvent HTTP/MCP as its own component
X. Other (please specify)

[Answer]: A. Existing Core/DTO/Notification only: EventService (process + lock), RulesService + JourneyNode (hydrate/collect), LoyaltyAccountService (bring-current + SaveLedgerExpirations), NotificationOutcome + `NotificationOutcomePayload` + existing `INotificationService` / `rest_api` adapter (2026-09-19, **Mode:** Chat)

## Q3. Interaction style between those blocks

A. Keep today’s in-process sync calls on the existing ProcessEvent path (recommended)
B. Introduce a new async/event hop for expire or webhook
X. Other (please specify)

[Answer]: A. Keep today’s in-process sync calls on the existing ProcessEvent path (2026-09-19, **Mode:** Chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the catalog and ADRs?

- Looks correct
- Request changes

[Answer]: Looks correct
