# MVP engine closeout Implementation Plan

> **Execution:** After approval, say execute and the intent name.
> `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not**
> use superpowers:subagent-driven-development. Linear unit issues land before
> any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Close the four engine gaps that block the stated MVP: tree-wide historical/taxonomy hydrate, earn-date-preserving PAT cascade, expire-on-process before rules, and `NotificationOutcome` firing tenant `rest_api` webhooks.

**Architecture:** Stay on the existing onion. Hydrate walks `JourneyNode` earn RuleSets plus NavConstraints. Cascade keeps `EarnDate` and computes dest `ExpirationDate` from that earn (or dest end date). `ProcessEventInternalAsync` calls `BringLoyaltyAccountPointsCurrentInternalAsync` after account populate. `NotificationOutcome` binds `NotificationConfigId`; `RulesEngineState` carries `INotificationService`; award loop unchanged. No UX, no hosted sweep, no new capability ids.

**Tech Stack:** net8.0 `Journeys.DTO` / `Core` / `API` / `Notification` / `Tests`. Existing `RestApiAdapter`. No new hosts.

**Spec:** `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- Controllers stay thin. Notification adapters stay in `Journeys.Notification`. No Core Azure SDK.
- Do not change `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)` signature.
- Do not add email/Twilio/data_pipeline adapters. Do not revive MassTransit process-event publish.
- Do not add a hosted expiration sweep. Do not add `/points/expire`. Do not change UX Expire-as-withdrawal this increment.
- Do not implement `WorkflowOutcome` / `RuleStateOutcome`. Do not promote `ThirdPartyOutcome`.
- Ledger date math is human-gated. Do not redesign `PointLedgerTypeStrings`.
- `Journeys.UX` is out of this plan.
- Do not log secrets, full event payloads, webhook auth headers, or raw audit JSON.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Do not name a product tenant in new copy, specs, or comments. Route templates may keep `{tenantId}`.
- Before Construction stage complete: `.\scripts\aidlc-agent-verify-sensor.ps1` (add `-RunTests` when tests changed). Then `docs-impact` and `graph-impact` must pass.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.Core/RulesEngine/Journey/JourneyNode.cs` | `CollectEarnAndNavRulesOfType<T>` (or equivalent) over Rules + NavConstraint + Children |
| `Journeys.Core/Services/RulesService.cs` | HydrateState uses the tree walk; set `state.NotificationService` |
| `Journeys.Core/RulesEngine/Engine/RulesEngineState.cs` | `INotificationService? NotificationService` |
| `Journeys.Core/Services/LoyaltyAccountService.cs` | `SaveLedgerExpirations` earn-date formula; remove UtcNow+100 default on move |
| `Journeys.Core/Services/EventService.cs` | Bring points current after populate, before `ProcessCampaignsAsync` |
| `Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs` | `NotificationConfigId`; implement Calculate/Award |
| `Journeys.DTO/Models/NotificationOutcomePayload.cs` | Closed webhook body |
| `Journeys.Core/Configuration/ConfigureNotifications.cs` | Always register `INotificationService` |
| `Journeys.API/Mcp/RulesEngineMcpContractSummary.cs` | MatrixVersion `2026-09-18`; NotificationConfigId critical row |
| `Journeys.Tests/RulesEngine/Journey/JourneyNodeCollectRulesTests.cs` | Tree walk unit tests |
| `Journeys.Tests/Services/RuleServiceTests.cs` | Child-node historical hydrate + second event |
| `Journeys.Tests/RulesEngine/Outcomes/ExpirePointsOutcomeTests.cs` | Lifecycle expects earn+365, not move+365 |
| `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs` | Direct cascade formula if easier than outcome tests alone |
| `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs` | Calculate/Award with fake `INotificationService` |
| `Journeys.Tests/Services/EventServiceBringPointsCurrentTests.cs` | Or equivalent helper test — expire-before-rules |
| `docs/product/ontology/outcome.md` | Notification implemented |
| `docs/product/ontology/loyalty-account.md` | Earn-date lifespan; ProcessEvent bring-current |
| `docs/product/ontology/rule.md` | Tree hydrate |
| `docs/product/use-cases/process-event.md` | Webhook + bring-current |
| `docs/product/graph/path-map.yaml` | `Journeys.Notification` meaningOptional false |
| `docs/platform/runtime.md` | Only if DI note is wrong today |

---

### Task 1: Tree-wide hydrate

**Files:**
- Modify: `Journeys.Core/RulesEngine/Journey/JourneyNode.cs`
- Modify: `Journeys.Core/Services/RulesService.cs` (`HydrateState` ~660–672)
- Create: `Journeys.Tests/RulesEngine/Journey/JourneyNodeCollectRulesTests.cs`
- Modify: `Journeys.Tests/Services/RuleServiceTests.cs` and/or `Journeys.Tests/TestCampaignFactory.cs`

**Interfaces:**
- Consumes: existing `RuleSet.FlattenToRulesOfType<T>`, `SimpleNavigationCriteria.NavConstraint`
- Produces: `JourneyNode.CollectEarnAndNavRulesOfType<T>()` used by `HydrateState` for `HistoricalRule` and `TaxonomicRule`

- [ ] **Step 1: Write failing collect-rules tests**

Cover: root RuleSet historical; child RuleSet historical; NavConstraint historical on a child; empty Rules does not throw.

- [ ] **Step 2: Implement `CollectEarnAndNavRulesOfType<T>`**

Walk `Rules` (each RuleSet), existing nav constraints, then `Children`. Do not break `FlattenToRulesOfType` callers unless you intentionally unify and update all call sites.

- [ ] **Step 3: Point `HydrateState` at the helper**

Replace `campaign.Journey.Rules` only scans for both historical and taxonomy lists. Keep TTL/decay/upsert behavior.

- [ ] **Step 4: Add a RuleService test with a child-node HistoricalRule**

Two events; second event sees count/TTL from the child RuleSet. Run the new + existing `RuleServiceTests` / `HistoricalRuleTests`.

- [ ] **Step 5: Verify**

`dotnet test --filter "FullyQualifiedName~JourneyNodeCollectRulesTests|FullyQualifiedName~RuleServiceTests|FullyQualifiedName~HistoricalRuleTests"`

---

### Task 2: Earn-date-preserving cascade (human-gated ledger)

**Files:**
- Modify: `Journeys.Core/Services/LoyaltyAccountService.cs` (`SaveLedgerExpirations`, ~2460–2486)
- Modify: `Journeys.Tests/RulesEngine/Outcomes/ExpirePointsOutcomeTests.cs`
- Create (if needed): `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs`

**Interfaces:**
- Consumes: `LedgerEntry.EarnDate`, dest `PointAccountType.PointsLifespanDays` / `PointsLifespanEndDate`
- Produces: dest `ExpirationDate` per spec §5.3; `EarnDate` unchanged

- [ ] **Step 1: Write failing cascade assertion**

After Escrow (30d) → Spendable (365d), assert `EarnDate` unchanged and Spendable expiration equals `EarnDate + 365 days` (not approximately `UtcNow + 365`).

- [ ] **Step 2: Change `SaveLedgerExpirations` date math**

Remove `UtcNow.AddDays(dest.PointsLifespanDays ?? 100)` as the move base. Apply spec §5.3. Keep dest ledger move + ETag upsert.

- [ ] **Step 3: Update existing PointLifecycle tests**

Any test that assumed the clock reset on hop must use earn-date expectations.

- [ ] **Step 4: Verify**

`dotnet test --filter "FullyQualifiedName~ExpirePointsOutcomeTests|FullyQualifiedName~LoyaltyAccountExpirationCascadeTests|FullyQualifiedName~PointAccountTypeValidatorTests"`

---

### Task 3: Expire-on-process before rules

**Files:**
- Modify: `Journeys.Core/Services/EventService.cs` (`ProcessEventInternalAsync` after `PopulateLoyaltyAccountAsync`)
- Create: focused test under `Journeys.Tests/Services/` (EventService helper or stubbed process)

**Interfaces:**
- Consumes: `ILoyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync`
- Produces: `loyaltyAccount.PointLedgers` current before `ProcessCampaignsAsync`

- [ ] **Step 1: Write a failing test**

Account with a due Escrow entry (`ExpirationDate <= UtcNow`, `ExpiresTo` = Spendable). After the process-event hydrate path, ledgers have moved **before** rules would read `PointBalanceProvider`.

If full `EventService` construction is too heavy, extract a small internal method `EnsurePointsCurrentForProcessAsync` and test that; `ProcessEventInternalAsync` must call it in the specified place.

- [ ] **Step 2: Call bring-current on the success path**

After populate, if `loyaltyAccount != null`, assign `PointLedgers` from `BringLoyaltyAccountPointsCurrentInternalAsync`. Draft verification included. Do not throw away populate failures.

- [ ] **Step 3: Verify**

`dotnet test --filter "FullyQualifiedName~EventServiceBringPointsCurrent|FullyQualifiedName~UserPointsTests"`

---

### Task 4: NotificationOutcome → tenant webhook

**Files:**
- Create: `Journeys.DTO/Models/NotificationOutcomePayload.cs`
- Modify: `Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs`
- Modify: `Journeys.Core/RulesEngine/Engine/RulesEngineState.cs`
- Modify: `Journeys.Core/Services/RulesService.cs` (set `NotificationService` when building state)
- Modify: `Journeys.Core/Configuration/ConfigureNotifications.cs`
- Modify: `Journeys.API/Mcp/RulesEngineMcpContractSummary.cs`
- Create: `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs`
- Modify: existing MCP contract tests if they snapshot `MatrixVersion` or CriticalRows

**Interfaces:**
- Consumes: `INotificationService.GetNotificationConfigAsync`, `SendNotificationAsync(tenantId, configId, payload)`
- Produces: HTTP POST via existing `RestApiAdapter` when Award runs

- [ ] **Step 1: Write failing NotificationOutcome tests**

Cases from spec §6. Fake `INotificationService` on `RulesEngineState`. Assert payload field names (camelCase JSON is fine if the default serializer already camelCases DTO properties — match existing notification send).

- [ ] **Step 2: Add `NotificationOutcomePayload` and outcome properties**

`NotificationConfigId` required. Calculate/Award per spec §5.1. Inject logger like other outcomes if needed.

- [ ] **Step 3: Wire state + DI + MCP row**

Always register `INotificationService`. `MatrixVersion = "2026-09-18"`. Critical row id `notification_config_id`, kind `NotificationOutcome`, field `NotificationConfigId`, violation `TIER_A_NOTIFICATION_MISSING_CONFIG_ID`.

- [ ] **Step 4: Verify**

`dotnet test --filter "FullyQualifiedName~NotificationOutcomeTests|FullyQualifiedName~RulesEngineMcpContract"`

If no MCP contract test filter matches, run the existing MCP/contract test project filter already used in-repo.

---

### Task 5: Docs and graph sync

**Files:**
- Modify: `docs/product/ontology/outcome.md`
- Modify: `docs/product/ontology/loyalty-account.md`
- Modify: `docs/product/ontology/rule.md`
- Modify: `docs/product/use-cases/process-event.md`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `docs/platform/runtime.md` only if needed

- [ ] **Step 1: Update ontology and use case to match shipped behavior**

Notification is implemented. Lifespan is from earn date. ProcessEvent brings points current. Hydrate walks the tree. Do not describe the hosted sweep as current.

- [ ] **Step 2: `Journeys.Notification` path-map `meaningOptional: false`**

- [ ] **Step 3: Run impact + verify**

```powershell
.\scripts\docs-impact.ps1
.\scripts\graph-impact.ps1
.\scripts\aidlc-agent-verify-sensor.ps1 -RunTests
```

Non-zero: halt and fix. Do not claim the increment done.

---

## Suggested AI-DLC unit boundaries

Units-generation should stay close to these tasks (do not copy checkboxes onto Linear):

1. Tree-wide hydrate (Task 1)
2. Earn-date cascade (Task 2, human-gated ledger)
3. Expire-on-process (Task 3)
4. NotificationOutcome webhook (Task 4)
5. Docs/graph (Task 5) — may fold into the last code unit if reviewers prefer fewer stories

## Out of plan (follow-on specs)

Hosted idle-account sweep; spend-API `IsSpendable` guard; UX PAT/notification/process-event; pagination; generic bulk; JourneyNavigator promotion tests; email/Twilio.
