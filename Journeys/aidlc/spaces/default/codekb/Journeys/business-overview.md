# Business Overview

Synthesized from the developer scan for intent `mvp-engine-closeout` (NO_STORE, first full scan). Product meaning stays in `docs/product/`. Code is canonical when ontology and implementation disagree. Ingested, not redesigned: `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md` and `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md`.

## Purpose

Journeys is a multi-tenant **signal response engine** (program runtime). A tenant owns event models, journey graphs, and declarative rules. Inbound customer events are evaluated; on a positive evaluation the engine delivers **outcomes** (points, tags, intended notifications, journey membership). Every business operation is **tenant-scoped** (`TenantId`).

This increment does **not** invent capability ids. The engine already implements:

| Capability id | Role in the stated MVP |
|---|---|
| `event-models` | Homogeneous inbound payload, wrapper persist, process-event |
| `campaigns` | Tenant program that owns a journey |
| `journeys` | Node graph, navigation, node RuleSets |
| `rules-engine` | Evaluate + hydrate historical/taxonomy state |
| `outcomes` | Calculate then award (deposit / spend / expire / tag / notification stub) |
| `campaign-agent` | Authoring host (out of this increment’s engine closeout) |
| `mcp-api` | Contract summary / authoring gates |

## Domain

Loyalty-program operators configure **point account types** (PATs), journeys, and rules. A typical stated program: hold points in Escrow for 30 days, then move to Spendable, then expire to Expired **one year after earn**. Process-event is the job that makes that program true for the account that just received an event.

Personas from the process-event use case: `program-operator`, `technical-buyer`.

Bounded language (do not synonymize): event model, campaign, journey, rule / RuleSet, outcome, loyalty account, ledger entry, notification config, tenant.

## Key Functionality (current code)

1. **Process an event** — `EventService.ProcessEventInternalAsync` populates the loyalty account, loads affiliated campaigns, and runs `RulesService.ProcessCampaignsAsync`. Wrapper state (`appliedcampaigns`, `outcomestates`, …) is the idempotence store. See [api-documentation.md](api-documentation.md) and [architecture.md](architecture.md).
2. **Navigate and evaluate** — `JourneyNavigator` / `JourneyNode.ProcessAsync` walk nodes; RuleSets calculate outcomes; the award loop calls `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)` (signature must not change).
3. **Point ledgers** — `LoyaltyAccountService` deposits, withdraws, expires, and cascades via `ExpiresToPointAccountTypeId`. `BringLoyaltyAccountPointsCurrentInternalAsync` already expires due entries (`ExpirationDate <= UtcNow`) on reconcile / GET-like paths. Ledger date math is **human-gated**. Do not redesign `PointLedgerTypeStrings`.
4. **Tenant webhooks** — CRUD on `NotificationController` plus `RestApiAdapter` (`adapterType: rest_api` only). Manual send and MassTransit job consumers (`EventProcessedJobConsumer`, `PointsChangedJobConsumer`) can POST. `NotificationOutcome` does **not** fire (Calculate/Award return null).
5. **MCP contract matrix** — `RulesEngineMcpContractSummary.Build()` pins `MatrixVersion = "2026-06-20"`. `NotificationOutcome` is listed in `OutcomeKinds` but has no `NotificationConfigId` critical row.

## Intent-relevant business gaps (confirmed in code)

The approved closeout is the first remaining-MVP increment. The scan localized all four approved gaps in current Core. Ontology `outcome.md` still matches the unimplemented webhook; **code remains canonical**.

| Gap | Business failure | Code locus (scan) |
|---|---|---|
| **G1** Webhook on outcome fire | A Live RuleSet with `NotificationOutcome` never POSTs the tenant webhook | `NotificationOutcome.cs` 21–31 return null; no `NotificationConfigId`; `RulesEngineState` has no `INotificationService` |
| **G2** Expire before this event’s rules | A 30-day Escrow hold does not release before this turn’s navigation / `PointBalanceProvider` | `ProcessEventInternalAsync` 867–929: populate → campaigns, no `BringLoyaltyAccountPointsCurrentInternalAsync` |
| **G3** Earn-date cascade | Spendable 365 starts at the Escrow→Spendable **move**, not at **earn** | `SaveLedgerExpirations` 2484–2486: dest expiration = `UtcNow + (days ?? 100)` |
| **G4** Tree hydrate | Child-node and NavConstraint historical/taxonomy rules skip TTL decay | `HydrateState` 660–672 reads only `campaign.Journey.Rules` |

## Non-goals (this intent)

Email / Twilio / data_pipeline adapters (DTO enums stay); MassTransit process-event republish in `EventService` (commented out — do not revive); `WorkflowOutcome` / `RuleStateOutcome` behavior; promoting `ThirdPartyOutcome`; hosted idle-account sweep; `/points/expire`; `Journeys.UX` product code; new capability ids; second graph / Neo4j; `PointLedgerTypeStrings` redesign.

## Cross-reference

Package map and health: [component-inventory.md](component-inventory.md). Stack versions: [technology-stack.md](technology-stack.md). Quality and missing tests: [code-quality-assessment.md](code-quality-assessment.md).
