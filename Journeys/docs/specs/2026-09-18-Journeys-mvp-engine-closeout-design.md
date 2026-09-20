# Design: MVP engine closeout (notifications, PAT cascade clock, journey hydrate)

**Date:** 2026-09-18  
**Status:** Approved (human 2026-09-18, execute mvp-engine-closeout)  
**Scope:** Close the four engine gaps that block the stated MVP: outcome-fired webhooks, expire-on-process before rules, earn-date-preserving PAT cascade, and tree-wide historical/taxonomy hydrate. Extends existing capabilities `outcomes`, `journeys`, `rules-engine`, `event-models`. No new capability ids.  
**Depends on:** `docs/product/ontology/outcome.md`, `loyalty-account.md`, `journey.md`, `rule.md`, `event-model.md`, `docs/product/use-cases/process-event.md`. Checkup: conversation 2026-09-18 (event / journey / outcomes / notifications / PAT).  
**Placement authority:** `docs/product/` and `AGENTS.md`.  
**If this spec and onion rules disagree:** Core owns business logic. Controllers stay thin. `Journeys.Notification` stays adapters only. `Journeys.UX` is out of this increment.  
**If this spec and `loyalty-account.md` / `outcome.md` disagree:** after implementation, **code is canonical** — update those ontology files in the same change.

Ledger / money-like outcomes stay a **human-gated** risk surface (`AGENTS.md`). Units that change `SaveLedgerExpirations` or expiration dates require human review before merge.

This spec is the **first** remaining-MVP increment. Follow-on work is listed in §7 and is **not** this intent.

---

## 1. Problem and goals

The single-event pipeline already processes tenant models, affiliates campaigns, navigates, and awards deposit / spend / expire / tag. The stated MVP still fails on four engine seams:

1. `NotificationOutcome` Calculate/Award return null. Tenant webhook CRUD and `RestApiAdapter` exist but never run when a RuleSet is true.
2. `ProcessEvent` does not call `BringLoyaltyAccountPointsCurrent`. A 30-day Escrow hold does not release before this event's navigation/rules unless some other path already touched the account.
3. PAT cascade sets the next `ExpirationDate` from `UtcNow + dest.PointsLifespanDays`, so a Spendable PAT with 365 days starts its clock at the Escrow→Spendable **move**, not at **earn**. The stated program is: hold 30 days, then expire to Expired **1 year after earning**.
4. `RulesService.HydrateState` collects `HistoricalRule` / `TaxonomicRule` only from `campaign.Journey.Rules` (root RuleSets). Child-node earn rules and nav-constraint historical rules do not get TTL decay. Later events do not benefit correctly.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Webhook on outcome fire** | A Live RuleSet with `NotificationOutcome` + tenant `NotificationConfigId` (`adapterType: rest_api`) POSTs the closed payload when the RuleSet is true and `CalculateOnly` is false. Manual `SendNotification` still works. |
| G2 | **Expire before this event's rules** | `ProcessEventInternalAsync` brings the account's points current **after** account load and **before** `ProcessCampaignsAsync`, so navigation and `PointBalanceProvider` see released hold balances. |
| G3 | **Earn-date cascade** | Moving entries via `ExpiresToPointAccountTypeId` **keeps `EarnDate`**. Next `ExpirationDate` is `EarnDate + dest.PointsLifespanDays`, or dest `PointsLifespanEndDate` if set. Escrow 30 → Spendable 365 → Expired yields expire-to-Expired at earn + 365 days. |
| G4 | **Tree hydrate** | Historical and taxonomic rules on **any** node RuleSet and on **NavConstraint** trees are TTL-decayed before eval, same as today's root RuleSets. |

### Non-goals (this spec)

- Email / Twilio / data_pipeline adapters (DTO enums stay; factory stays `rest_api` only)
- Tenant-wide “notify on every processed event” MassTransit republish in `EventService`
- `WorkflowOutcome` / `RuleStateOutcome` runtime behavior
- `ThirdPartyOutcome` (not an `OutcomeBase`; do not promote)
- Hosted batch expiration sweep for accounts that receive **no** events (follow-on)
- Live campaign / tag pagination past 100
- Generic `ProcessBulk` (order CSV stays legacy)
- Operator UX: model builder, process-event console, PAT admin CRUD, notification-config screens, UX Expire tab rewrite
- Spend-API enforcement of `IsSpendable` (follow-on)
- New capability ids, second graph, Neo4j
- Redesign of `PointLedgerTypeStrings` or a second points store
- `Journeys.UX` product code this increment

---

## 2. Approaches considered

**Packaging**

| Approach | Trade-off |
|----------|-----------|
| **A. One spec / one Classic intent for the four engine gaps (recommended)** | One Linear-projected unit set; shared `ProcessEvent` / `RulesService` blast radius. Larger than a single-outcome change; still smaller than “all remaining MVP.” |
| B. Four separate specs/intents | Cleaner gates; four Classic bootstraps and four Linear upserts. Hydrate and expire-on-process would land after notifications and leave the stated story half-true. |
| C. Entire remaining MVP (UX, pagination, bulk, email) in one spec | Too large for one intent; mixes human-gated ledger work with shell work. |

**Notification wiring**

| Approach | Trade-off |
|----------|-----------|
| **A. `NotificationOutcome.NotificationConfigId` + `RulesEngineState.NotificationService` (recommended)** | Extends existing award loop. No new outcome Kind. Tenant configs stay the webhook source of truth. |
| B. Special-case Kind in `RulesService` after award | Duplicates dispatch outside the outcome type. |
| C. Inline URL on the outcome | Bypasses tenant config, secrets in campaign JSON, second webhook store. |

**Expiration clock**

| Approach | Trade-off |
|----------|-----------|
| **A. ProcessEvent bring-current only (recommended this increment)** | Event-active accounts release holds before this turn's navigation. Inactive accounts stay lazy until GET/deposit/withdraw/reconcile (already implemented). |
| B. New hosted sweep this increment | Needs a cheap account-with-due-expirations query that does not exist; new infra. Follow-on spec. |
| C. Campaigns must attach `ExpirePointsOutcome` on every event | Works but is authoring burden; PAT lifespan would not mean what operators configure. |

**Cascade clock**

| Approach | Trade-off |
|----------|-----------|
| **A. Lifespan days are always from original `EarnDate` (recommended)** | PAT-only config matches “30-day hold, expire 1 year after earning”: Escrow `PointsLifespanDays=30`, Spendable `=365`, Expired terminal. |
| B. Lifespan days are time-in-bucket | Spendable 365 means 395 days from earn after a 30-day hold. Contradicts the stated program. |
| C. Keep UtcNow reset; require `ExpirePointsOutcome` by EarnDate for the 1-year hop | Leaves PAT config lying about the 1-year story. |

---

## 3. Decisions

| Topic | Choice |
|-------|--------|
| Intent packaging | One Classic intent. Four construction units (hydrate, earn-date cascade, expire-on-process, NotificationOutcome). |
| Webhook binding | `NotificationOutcome.NotificationConfigId` (GUID string). Required for Calculate to return a result. |
| Webhook transport | Existing tenant `NotificationConfig` with `AdapterType = rest_api`. Factory unchanged. |
| Service access | `RulesEngineState.NotificationService` (`INotificationService`), set by `RulesService` the same way `LoyaltyAccountService` is already on state. Do **not** change `AwardOutcomeAsync` signature. |
| Payload | Closed DTO `NotificationOutcomePayload` in `Journeys.DTO`. No raw event JSON, no ledger rows, no secrets. |
| Webhook failure | Non-fatal. Log. Return `OutcomeResult` with `IsAwarded = false`. Do not roll back sibling point/tag awards. At-least-once: reprocess may POST again. |
| CalculateOnly | Calculate may resolve/validate config; Award and HTTP send do not run. |
| Missing / inactive config | Calculate returns null (nothing earned). Do not throw the whole event. |
| EventModelId filter | If config.EventModelId is set and ≠ current `state.EventModelId`, Calculate returns null. |
| DI | `AddNotificationsServices` always registers `INotificationService`. Remove the `DisableDataLake` gate. |
| Expire-on-process | `BringLoyaltyAccountPointsCurrentInternalAsync` in `ProcessEventInternalAsync` after `PopulateLoyaltyAccountAsync`, before `ProcessCampaignsAsync`. Skip when account is invalid / not found. Do not add a hosted sweep. |
| Cascade formula | Preserve `LedgerEntry.EarnDate`. If dest `PointsLifespanEndDate` is set: that date. Else if dest `PointsLifespanDays` is set: `EarnDate.AddDays(days)`. Else: leave `ExpirationDate` unset (terminal / no further roll). Remove `UtcNow + (days ?? 100)` on the move. |
| Hydrate walk | New `JourneyNode` helper collects `HistoricalRule` / `TaxonomicRule` from every node's `Rules` RuleSets **and** `NavConstraint` trees, recursively through `Children`. `HydrateState` uses that helper, not `campaign.Journey.Rules` only. |
| MCP | Add critical row: `NotificationOutcome` requires `NotificationConfigId`. Bump `RulesEngineMcpContractSummary.MatrixVersion` to `2026-09-18`. |
| UX | None this increment. Agent/REST already upsert notification configs and PAT types. |
| Commit of this spec | Human only |

---

## 4. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.DTO/Models/          # NotificationOutcomePayload
  Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs
  Journeys.Core/RulesEngine/Engine/RulesEngineState.cs
  Journeys.Core/RulesEngine/Journey/JourneyNode.cs
  Journeys.Core/Services/RulesService.cs
  Journeys.Core/Services/EventService.cs
  Journeys.Core/Services/LoyaltyAccountService.cs   # SaveLedgerExpirations only
  Journeys.Core/Configuration/ConfigureNotifications.cs
  Journeys.API/Mcp/RulesEngineMcpContractSummary.cs
  Journeys.Notification/        # no new adapter types
  Journeys.Tests/
  docs/product/ontology/outcome.md
  docs/product/ontology/loyalty-account.md
  docs/product/ontology/rule.md
  docs/product/use-cases/process-event.md
  docs/platform/runtime.md      # only if notification registration notes change
  docs/product/graph/path-map.yaml
```

`Journeys.UX`, `Journeys.Agent` prompt-only files, and new hosted services are out.

---

## 5. Behavior

### 5.1 NotificationOutcome

**Shape (PascalCase keys, existing outcome casing):**

| Property | Meaning |
|----------|---------|
| `Kind` | `NotificationOutcome` |
| `NotificationConfigId` | Tenant notification config GUID. Required. |
| `EventId` / `EventType` / providers | Same as other outcomes; resolve on Calculate. |

**Calculate:** resolve event id/type; load config via `state.NotificationService`; apply EventModelId filter; return `OutcomeResult` (`IsAwarded = false`) or null.

**Award:** `SendNotificationAsync(tenantId, configId, payload)`. Payload fields (all strings/dates, no event body):

- `tenantId`, `loyaltyAccountId`, `extAccountId` (if present)
- `campaignId`, `ruleSetId`, `issuingOutcomeId`, `issuingOutcomeKind`
- `eventId`, `eventType`, `eventModelId`, `awardedAtUtc`

`RestApiAdapter` already JSON-serializes the object.

**Authoring:** campaign agent / MCP upserts the campaign with the outcome on a RuleSet (or nav outcomes). Operator creates the webhook config first via existing `NotificationController`. No inline URL on the outcome.

### 5.2 Expire before rules

After a valid loyalty account is populated for a processable event, load/expire due ledger entries (`ExpirationDate <= UtcNow`) through the existing `BringLoyaltyAccountPointsCurrentInternalAsync` path, assign `loyaltyAccount.PointLedgers`, then run campaigns. Draft verification uses the same order.

Do not call bring-current on the pre-save wrapper path before the account exists.

### 5.3 Earn-date-preserving cascade

`SaveLedgerExpirations` keeps `xentry.EarnDate`. Destination expiration:

1. Dest `PointsLifespanEndDate` not null/min → that instant.
2. Else dest `PointsLifespanDays` has a value → `EarnDate + days` (if `EarnDate` missing, use existing entry `ExpirationDate` only as a last resort; do **not** use `UtcNow` as the lifespan base).
3. Else clear or leave unset — Expired/Archive sinks typically have no `expiresTo` and no days.

The 100-day default on move is **removed**. Validator already requires lifespan/`expiresTo` pairing for rolling PATs.

**Configured example (MVP story):**

| PAT | LedgerType | IsSpendable | Days | ExpiresTo |
|-----|------------|-------------|------|-----------|
| Hold | Escrow | false | 30 | Spendable PAT id |
| Spendable | Spendable | true | 365 | Expired PAT id |
| Expired | Expired | false | — | none |

Earn 2026-01-01 → Hold until 2026-01-31 → Spendable until 2027-01-01 → Expired.

### 5.4 Tree hydrate

Before any `Evaluate`, collect historical/taxonomy rules from the full journey tree (earn RuleSets **and** navigation constraints). Existing TTL fetch, decay, `LoyaltyAccount.RuleState` load, and `finally` upsert stay. Root-only scan is a bug, not a behavior to preserve.

`JourneyNode.FlattenToRulesOfType<T>` today walks **NavConstraint + Children only** (not `Rules`). Either extend that method or add `CollectEarnAndNavRulesOfType<T>` and use it from `HydrateState`. Do not silently drop nav historical rules.

---

## 6. Testing

| Area | Required tests |
|------|----------------|
| NotificationOutcome | Calculate: missing config id → null; inactive/missing config → null; EventModelId mismatch → null; happy path result. Award: calls `INotificationService` with payload fields; service false → `IsAwarded` false, no throw. CalculateOnly never sends. |
| MCP | Contract summary includes `NotificationConfigId` critical row; existing deposit/spend rows unchanged. |
| Cascade | `ExpirePointsOutcomeTests` lifecycle (or dedicated `SaveLedgerExpirations` tests): after Escrow→Spendable, `EarnDate` unchanged and Spendable `ExpirationDate == EarnDate + 365d` (not `~UtcNow+365d`). |
| Hydrate | `RuleServiceTests` (or new): HistoricalRule on a **child** node RuleSet decays TTL and evaluates on a second event. Prefer extending `TestCampaignFactory` rather than a one-off campaign blob. |
| Expire-on-process | Unit or service test: account with due Escrow `ExpirationDate` is brought current before rules when `ProcessEventInternalAsync` / a extracted helper runs. If `EventService` is hard to construct, test a protected/internal helper or prove via `LoyaltyAccountService` + a focused EventService test with existing stubs. |

Existing `ExpirePointsOutcomeTests` PointLifecycle cases that assume UtcNow-reset must be updated to the earn-date formula — that is a required rewrite, not an optional extra.

---

## 7. Follow-on specs (not this intent)

1. Hosted expiration sweep for idle accounts.
2. `IsSpendable` enforced on withdraw APIs; UX Expire tab → expire service (not withdrawal).
3. PAT / notification-config / process-event operator UX.
4. Live campaign pagination; generic bulk ingest.
5. JourneyNavigator Transition/Exit/sibling tests and Bronze→Silver→Gold proof (engine already implemented).
6. Email / Twilio adapters.

---

## 8. Docs and graph (same change)

- `docs/product/ontology/outcome.md` — Notification Calculate/Award **implemented**; webhook via `NotificationConfigId`; failure non-fatal.
- `docs/product/ontology/loyalty-account.md` — lifespan days are from `EarnDate` across cascade hops; ProcessEvent brings points current before rules; no hosted sweep yet.
- `docs/product/ontology/rule.md` — HydrateState walks the journey tree (earn + nav).
- `docs/product/use-cases/process-event.md` — notifications fire from `NotificationOutcome`; points current before journey eval.
- `docs/product/graph/path-map.yaml` — `Journeys.Notification` prefix `meaningOptional: false` (outcomes).
- `docs/platform/runtime.md` — only if the notification DI note would otherwise be wrong.

Run `scripts/docs-impact.ps1` and `scripts/graph-impact.ps1` before the increment is done.

---

## 9. Risks

| Risk | Mitigation |
|------|------------|
| Ledger date change alters existing tenant balances | Human-gated unit. Tests lock the new formula. Call out in Linear AC. |
| Webhook reprocess doubles POSTs | Document at-least-once. Do not add a second idempotency store this increment. |
| `NotificationService` missing when DataLake disabled | Always register the service. |
| Award signature change breaks all outcomes | Put service on `RulesEngineState` instead. |

---

## 10. Execution

After this spec and `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md` are **Approved**, say `execute` plus an intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Linear unit issues land before any `Journeys.*` code. Agents do not merge.
