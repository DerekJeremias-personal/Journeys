# Requirements — MVP engine closeout

**Conversation language:** English  
**Intent:** `mvp-engine-closeout`  
**Depth:** Standard  
**Source spec:** `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md` (Approved)  
**Source plan:** `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md` (decomposition only)

## Sources

| Tag | Origin |
|-----|--------|
| [spec] | Approved design spec (G1–G4, payload, formula, non-goals) |
| [Q1]–[Q6] | `requirements-analysis-questions.md` |
| [practices] | Affirmed `team.md` / `project.md` (2026-09-19) |
| [codekb] | `business-overview.md`, `architecture.md`, `code-structure.md` |

## Intent analysis

A tenant program already processes events, affiliates campaigns, navigates journeys, and awards deposit / spend / expire / tag. The stated MVP still fails on four engine seams: webhooks never fire from a Live RuleSet, holds do not release before this event’s rules, PAT cascade clocks start at the move instead of at earn, and child-node / nav historical rules skip TTL decay.

This increment makes **one ProcessEvent** on an event-active account tell the truth for that program: hydrate the full journey tree, expire due ledgers under the existing account lock, cascade on earn-date math, and POST the tenant `rest_api` webhook when a `NotificationOutcome` is earned. Personas: `program-operator`, `technical-buyer`. Existing capability ids only: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`.

Success is the spec’s G1–G4 criteria plus the interview edges below. Ledger date math stays human-gated.

## Functional requirements

### FR1 Tree-wide hydrate [spec]

The system shall TTL-decay and load historical and taxonomic rules from every journey node’s earn RuleSets **and** NavConstraint trees, recursively through `Children`, before evaluate.

- **FR1.1** `HydrateState` shall not read only root `campaign.Journey.Rules`.
- **FR1.2** A HistoricalRule on a child-node RuleSet shall decay and evaluate on a later event the same way a root RuleSet does today.
- **FR1.3** A HistoricalRule on a NavConstraint shall be included; empty `Rules` shall not throw.
- **FR1.4** Existing TTL fetch, decay, `LoyaltyAccount.RuleState` load, and finally upsert shall stay.

**Pass:** Child-node and nav historical cases in `Journeys.Tests` fail on root-only hydrate and pass after the tree walk.  
**Fail:** Root-only scan remains, or nav historical rules are dropped.

### FR2 Earn-date-preserving cascade [spec] [Q1] [Q3]

The system shall keep `LedgerEntry.EarnDate` on an `ExpiresToPointAccountTypeId` hop and set dest `ExpirationDate` from earn (or dest end date), not from `UtcNow`.

- **FR2.1** If dest `PointsLifespanEndDate` is set: dest expiration is that instant.
- **FR2.2** Else if dest `PointsLifespanDays` is set: dest expiration is `EarnDate + days`. If `EarnDate` is missing, use the entry’s existing `ExpirationDate` only as last resort. Do not use `UtcNow` as the lifespan base. Remove `UtcNow + (days ?? 100)` on the move.
- **FR2.3** Else leave dest expiration unset (terminal / no further roll).
- **FR2.4** Configured example: Escrow 30 → Spendable 365 → Expired. Earn 2026-01-01 → hold until 2026-01-31 → Spendable until 2027-01-01 → Expired.
- **FR2.5** [Q1] Already-moved rows that used the old UtcNow clock stay as-is until they hop again. Only new cascade writes use FR2.1–FR2.3.
- **FR2.6** [Q3] If dest PAT id is missing or not loadable for the tenant: leave the entry in the source PAT, log, do not fail the event. Do not drop or archive it.

**Pass:** After Escrow→Spendable, `EarnDate` is unchanged and Spendable `ExpirationDate == EarnDate + 365d` when dest days is 365. Missing dest PAT leaves the source row.  
**Fail:** Dest clock is `~UtcNow + 365`, or a missing dest PAT fails ProcessEvent or deletes the row.

### FR3 Expire-on-process under the existing lock [spec] [Q2] [Q5] [Q6]

The system shall bring the account’s points current on ProcessEvent **after** a valid account is populated and **after** `TryLockAccount` in `ProcessCampaignsAsync` (or the equivalent single lock that already wraps rules), and **before** navigation / RuleSet evaluate / `PointBalanceProvider`.

- **FR3.1** Use existing `BringLoyaltyAccountPointsCurrentInternalAsync` (expire `ExpirationDate <= UtcNow`). Assign `loyaltyAccount.PointLedgers`.
- **FR3.2** Skip when the account is invalid or not found. Do not add a hosted sweep or `/points/expire`.
- **FR3.3** Do not call bring-current on the pre-save wrapper path before the account exists.
- **FR3.4** [Q5] No new lock or queue. Reuse `TryLockAccount`. Order: acquire lock → bring-current → rules.
- **FR3.5** [Q2] If bring-current throws: fail the whole ProcessEvent. Do not navigate or award that turn.
- **FR3.6** [Q6] GET / reconcile bring-current callers stay as they are this increment (`ExpirePoints` still locks when it finds due rows).

**Pass:** An account with a due Escrow row releases before this turn’s rules when ProcessEvent runs; tests prove order under the existing lock.  
**Fail:** Bring-current stays off ProcessEvent, or it runs unlocked before `ProcessCampaignsAsync`.

### FR4 NotificationOutcome webhook [spec] [Q4]

The system shall POST the tenant webhook when a Live RuleSet earns `NotificationOutcome` and `CalculateOnly` is false.

- **FR4.1** `NotificationOutcome.NotificationConfigId` (GUID string) is required for Calculate to return a result. No inline URL.
- **FR4.2** Transport is existing tenant `NotificationConfig` with `AdapterType = rest_api`. Factory unchanged. No email / Twilio / data_pipeline adapters.
- **FR4.3** `RulesEngineState.NotificationService` (`INotificationService`) is set by `RulesService` the same way `LoyaltyAccountService` is on state. Do not change `AwardOutcomeAsync` signature.
- **FR4.4** Calculate: resolve event id/type; load config; EventModelId filter (mismatch → null); missing / inactive config → null (do not throw the event). Happy path returns `OutcomeResult` with `IsAwarded = false` until Award.
- **FR4.5** Award: `SendNotificationAsync(tenantId, configId, payload)`. Closed DTO `NotificationOutcomePayload` in `Journeys.DTO/Models/`: `tenantId`, `loyaltyAccountId`, `extAccountId` (if present), `campaignId`, `ruleSetId`, `issuingOutcomeId`, `issuingOutcomeKind`, `eventId`, `eventType`, `eventModelId`, `awardedAtUtc`. No raw event JSON, ledger rows, or secrets.
- **FR4.6** `false` send: `IsAwarded = false`; do not roll back sibling point/tag awards; at-least-once on reprocess (no second idempotency store).
- **FR4.7** [Q4] If send **throws**: fail ProcessEvent by default. A host/appsettings (or equivalent) switch may change that to FR4.6 behavior (catch, log, `IsAwarded = false`). No new capability id.
- **FR4.8** CalculateOnly: Calculate may resolve/validate; Award and HTTP send do not run.
- **FR4.9** Always register `INotificationService` (`AddNotificationsServices`). Remove the `DisableDataLake` gate on that registration.
- **FR4.10** Award loop shall honor `IsAwarded = false` on the returned result (do not force true on any non-null Award).

**Pass:** Happy-path POST with the closed payload; missing config → null; EventModelId mismatch → null; `false` → `IsAwarded` false; throw fails ProcessEvent unless the switch is on; CalculateOnly never sends.  
**Fail:** Calculate/Award still return null always, or Award signature changes, or payload includes event body / secrets.

### FR5 MCP contract [spec]

The system shall add a critical row: `NotificationOutcome` requires `NotificationConfigId`. Bump `RulesEngineMcpContractSummary.MatrixVersion` to `2026-09-18`. Existing deposit/spend rows stay.

**Pass:** Contract summary test updates with the new version and critical row.  
**Fail:** Matrix stays `2026-06-20` with no NotificationConfigId row.

### FR6 Docs and graph in the same change [spec] [practices]

The increment shall update product meaning when code lands:

- `docs/product/ontology/outcome.md` — Notification Calculate/Award implemented; webhook via `NotificationConfigId`; failure rules per FR4.
- `docs/product/ontology/loyalty-account.md` — lifespan from `EarnDate` across hops; ProcessEvent bring-current before rules under the existing lock; no hosted sweep.
- `docs/product/ontology/rule.md` — HydrateState walks the journey tree (earn + nav).
- `docs/product/use-cases/process-event.md` — notifications fire; points current before journey eval.
- `docs/product/graph/path-map.yaml` — `Journeys.Notification` `meaningOptional: false`.
- `docs/platform/runtime.md` — only if the notification DI note would otherwise be wrong.

`scripts/docs-impact.ps1` and `scripts/graph-impact.ps1` must pass before the increment is done. Construction verify: `scripts/aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed).

## Non-functional requirements

### NFR1 Tenant isolation [practices] [spec]

Every business operation in this increment remains tenant-scoped (`TenantId`). Webhook send uses the event’s tenant and that tenant’s `NotificationConfig` only.

### NFR2 Secrets and payload hygiene [spec] [practices]

Do not log secrets, full event payloads, webhook auth headers, or raw audit JSON. The webhook body is the closed FR4.5 field list only.

### NFR3 At-least-once notifications [spec]

Reprocess may POST again. Do not add a second idempotency store this increment.

### NFR4 Ledger / money-like human gate [practices] [spec]

Units that change `SaveLedgerExpirations` or expiration dates require human review before merge. Do not redesign `PointLedgerTypeStrings`.

### NFR5 Concurrency [Q5] [codekb]

Same-account ProcessEvent continues to serialize on `TryLockAccount` (existing lease + retries). This increment adds no new lock, queue, or distributed mutex. Wrapper pre-save may still happen before the lock (today’s behavior).

### NFR6 Webhook-throw switch [Q4]

Default: throw fails ProcessEvent (fail closed). Optional host/appsettings (or equivalent already used for API flags) flips to non-fatal FR4.6. The switch is configuration, not a new capability id and not tenant UX this increment.

### NFR7 Test posture [practices]

Methodology `custom`: failing `Journeys.Tests` first on new closeout seams (hydrate/collect-rules, cascade clock, expire-before-rules under lock, NotificationOutcome Calculate/Award). Historical tests stay test-after. No in-repo coverage floor. Fake `INotificationService` on `RulesEngineState`; do not reference `Journeys.Notification` from tests; do not extend the `RulesService` constructor for that fake.

## Constraints

- Onion: controllers thin; Core owns rules; `Journeys.Notification` adapters only; `Journeys.UX` out of increment.
- No new capability ids. Do not promote `ThirdPartyOutcome`. Do not implement `WorkflowOutcome` / `RuleStateOutcome`.
- Do not change `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)`.
- Do not revive MassTransit process-event publish in `EventService`.
- Do not add terraform / Databricks / AWS-CDK work in this tree.
- Do not edit Auth0 tenant `"hayward"`.
- Linear: one issue per AI-DLC unit after units-generation; human `-ApproveCreate` before any `Journeys.*` product code.
- Agents do not merge or release.

## Assumptions

| ID | Assumption | Status |
|----|------------|--------|
| A1 | Q4 switch lives in existing host/appsettings (or equivalent), default fail-closed | Confirmed at interview; exact key name is Construction |
| A2 | `TryLockAccount` in `ProcessCampaignsAsync` is the lock FR3 uses; lease/retry stay as today | Confirmed in code scan |
| A3 | GET/reconcile unlocked-unless-due behavior is acceptable until a follow-on | Confirmed [Q6] |
| A4 | Already-cascaded UtcNow clocks are acceptable until those rows hop again | Confirmed [Q1] |

## Out of scope

- Email / Twilio / data_pipeline adapters (DTO enums stay)
- Tenant-wide notify-on-every-event MassTransit republish
- Hosted idle-account expiration sweep; `/points/expire`
- `IsSpendable` enforcement on withdraw APIs; UX Expire-tab rewrite
- Live campaign / tag pagination past 100; generic `ProcessBulk`
- Operator UX (model builder, process-event console, PAT admin, notification-config screens)
- New capability ids, second graph, Neo4j
- `PointLedgerTypeStrings` redesign; second points store
- `Journeys.UX` product code
- Wrapping GET/reconcile bring-current in an outer `TryLockAccount` [Q6]
- Rewriting historical dest `ExpirationDate` on already-moved rows [Q1]

## Open questions

None blocking. Construction may name the Q4 appsettings key. Follow-on: GET/reconcile outer lock; hosted sweep; spend-API `IsSpendable`; operator UX.
