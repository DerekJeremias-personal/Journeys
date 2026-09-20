# Architecture decisions — MVP engine closeout

Significant Domain Design choices only. Deployment topology is Units Generation. Tech stack and NFR patterns stay later.

## ADR-001: Catalogue existing onion blocks; add no components

- **Context** — The increment closes four engine gaps on types that already exist (`EventService`, `LoyaltyAccountService`, `RulesService` / `JourneyNode`, `NotificationOutcome`, `INotificationService`). The stage condition would allow a skip. Units Generation still needs named owners.
- **Decision** — Execute a thin catalogue of those existing blocks. Do not invent a capability, host, AWS stack, or UI component. `NotificationOutcomePayload` and `RulesEngineState.NotificationService` are additions inside existing blocks.
- **Consequences** — Construction maps units onto live types. Reviewers cannot treat a missing catalogue as license to greenfield. The catalogue does not authorize a new microservice or a second notification write path.
- **Alternatives Rejected** — Skip Domain Design and let Units Generation map stories from the plan only (Q1-B): cheaper, but leaves no named component owners for traceability. Split a new Notification component out of `Journeys.Notification` (Q2-B): contradicts affirmed adapters-only Notification. Treat ProcessEvent HTTP/MCP as its own component (Q2-C): controllers stay thin; Core owns the seams.

## ADR-002: Keep in-process sync ProcessEvent calls

- **Context** — Expire-on-process and webhook fire must be visible on the same event. MassTransit process-event publish in `EventService` is commented out and must stay commented.
- **Decision** — EventService → LoyaltyAccountService / RulesService → NotificationOutcome remain synchronous in-process calls on the existing ProcessEvent path.
- **Consequences** — Same-account events still serialize on `TryLockAccount`. No new queue, bus hop, or hosted sweep. Webhook latency can fail the event (default) unless the host switch is on.
- **Alternatives Rejected** — A new async/event hop for expire or webhook (Q3-B): reopens the out-of-increment bus path and can hide this-turn spend / webhook from the operator.

## ADR-003: NotificationOutcome owns the closed payload; adapter stays external

- **Context** — Award must POST a closed DTO without changing `AwardOutcomeAsync` or adding `HttpClient` on the outcome. `Journeys.Notification` is adapters only.
- **Decision** — `NotificationOutcome` owns Calculate/Award and `NotificationOutcomePayload`. Transport is the existing `INotificationService` / `rest_api` adapter, listed as an external dependency, not a fifth catalogue component.
- **Consequences** — Tests fake `INotificationService` on `RulesEngineState`. Tests must not reference `Journeys.Notification`. MCP `NotificationConfigId` stays AC on US4.1.
- **Alternatives Rejected** — New Notification component (Q2-B). Inline URL on the outcome. Changing the Award signature to inject the service.

## ADR-004: Bring-current stays under the existing ProcessCampaignsAsync lock

- **Context** — Spec G2 placed bring-current in `ProcessEventInternalAsync` before `ProcessCampaignsAsync` (unlocked). Requirements FR3 / Q5 place it after `TryLockAccount` inside `ProcessCampaignsAsync`.
- **Decision** — Domain ownership: `EventService` owns call order (lock → bring-current → `RulesService`). `LoyaltyAccountService` owns the bring-current and cascade implementations. Construction must not implement the unlocked pre-campaign placement.
- **Consequences** — Two ProcessEvents cannot both expire then contend. GET/reconcile outer lock stays deferred. Human-gated ledger math stays on `LoyaltyAccountService`.
- **Alternatives Rejected** — Unlocked bring-current before `ProcessCampaignsAsync` (spec G2 / plan Task 3 placement). A new mutex or expire API.
