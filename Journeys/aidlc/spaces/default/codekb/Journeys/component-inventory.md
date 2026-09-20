# Component Inventory

Headings below are the `analyzed.components` names. Health: **healthy** / **at-risk** / **degraded**. Depth: Standard — one record per component; sequences live in [architecture.md](architecture.md).

## Engine — journey and rules

### JourneyNode
- **Path:** `Journeys.Core/RulesEngine/Journey/JourneyNode.cs`
- **Responsibility:** Node process, RuleSet evaluation, tree flatten helpers.
- **Depends on:** RuleSets, nav criteria, `OutcomeBase`, children.
- **Health:** **at-risk**. `FlattenToRulesOfType<T>` (234–257) walks `NavConstraint` via `OfType<T>()` (drops nested `HistoricalRule`) + `Children`; does **not** walk `Rules`. Closeout adds `CollectEarnAndNavRulesOfType<T>` (Rules + composite-flatten nav + Children) without silently dropping nested historical rules.

### SimpleNavigationCriteria
- **Path:** `Journeys.Core/RulesEngine/Journey/SimpleNavigationCriteria.cs`
- **Responsibility:** Nav constraint holder used during flatten/hydrate.
- **Depends on:** Rule-like `NavConstraint` trees.
- **Health:** **healthy** as a type; hydrate must flatten its composites (see JourneyNode).

### JourneyNavigator
- **Path:** `Journeys.Core/RulesEngine/Journey/JourneyNavigator.cs`
- **Responsibility:** Membership navigation (Entry/Exit/Transition).
- **Depends on:** `JourneyNode`, `RulesEngineState`.
- **Health:** **healthy** for this increment. Transition/Exit/sibling proof is follow-on, not closeout.

### RulesEngineState
- **Path:** `Journeys.Core/RulesEngine/Engine/RulesEngineState.cs`
- **Responsibility:** Per-evaluation state bag (account, event, services).
- **Depends on:** `ILoyaltyAccountService` (present). Unused usings: `Microsoft.Azure.Amqp.Framing`, `Microsoft.EntityFrameworkCore.ValueGeneration.Internal`.
- **Health:** **at-risk**. Has `LoyaltyAccountService`; **no** `NotificationService`. `NavigatePayload` copies loyalty service only (RulesService 248–249). Closeout adds `INotificationService?` the same way — do not change `AwardOutcomeAsync` signature.

### RuleSet
- **Path:** `Journeys.Core/RulesEngine/RuleSet.cs`
- **Responsibility:** Earn-rule grouping + `FlattenToRulesOfType<T>` (composite-aware).
- **Depends on:** `RuleBase` tree.
- **Health:** **healthy**. `HydrateState` should use this flatten on **each node’s** RuleSets, not only `campaign.Journey.Rules`.

### RuleBase
- **Path:** `Journeys.Core/RulesEngine/Rules/RuleBase.cs`
- **Responsibility:** Shared rule evaluate/hydrate surface.
- **Health:** **healthy**.

### HistoricalRule
- **Path:** `Journeys.Core/RulesEngine/Rules/HistoricalRule.cs`
- **Responsibility:** TTL/decay historical counts; requires hydrate before eval.
- **Depends on:** `LoyaltyAccount.RuleState`, `HydrateState`.
- **Health:** **at-risk** at child/nav placements until G4. Root-campaign fixtures in `RuleServiceTests` will not catch the bug.

### PointBalanceProvider
- **Path:** `Journeys.Core/RulesEngine/Providers/PointBalanceProvider.cs`
- **Responsibility:** Read `LoyaltyAccount.PointLedgers`; if null, `GetLoyaltyAccountPointsAsync` (30–36) **without** resettle/expire.
- **Health:** **at-risk**. Relies on G2 bring-current so process-event ledgers are already current.

## Engine — outcomes

### OutcomeBase
- **Path:** `Journeys.Core/RulesEngine/Outcomes/OutcomeBase.cs`
- **Responsibility:** Calculate then Award. Award signature is load-bearing.
- **Health:** **healthy**. Do not change `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)`.

### NotificationOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs`
- **Kind:** `OutcomeKindDiscriminators.NotificationOutcome` (exists).
- **Responsibility:** Intended tenant webhook on positive RuleSet.
- **Health:** **degraded (G1)**. Calculate/Award `//TODO` and `return null` (21–31). No `NotificationConfigId`. Closed payload DTO missing. `JourneyNode.ProcessAsync` never adds an `OutcomeResult`.

### TagOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/TagOutcome.cs`
- **Responsibility:** `TagLoyaltyAccountAsync` on positive evaluation.
- **Health:** **healthy**. Pattern for Calculate/Award that Notification should follow.

### DepositPointsOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/DepositPointsOutcome.cs`
- **Responsibility:** Deposit against PAT GUIDs; `PointsPerDollar` × provider.
- **Health:** **healthy**. MCP deposit rows must remain when matrix bumps.

### WorkflowOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/WorkflowOutcome.cs`
- **Health:** **degraded** stub (`return null`). **Out of increment — do not implement.**

### RuleStateOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/RuleStateOutcome.cs`
- **Health:** **degraded** stub. **Out of increment — do not implement.**

### ThirdPartyOutcome
- **Path:** `Journeys.Core/RulesEngine/Outcomes/ThirdPartyOutcome.cs`
- **Health:** **at-risk** as a type: **not** an `OutcomeBase`. Do not promote.

## Core services

### RulesService
- **Path:** `Journeys.Core/Services/RulesService.cs`
- **Responsibility:** `ProcessJourneyAsync` / `HydrateState` / award loop / `NavigatePayload`.
- **Depends on:** journey tree, outcomes, `ILoyaltyAccountService`. Constructor does **not** take `INotificationService`.
- **Health:** **degraded** for G4 (`HydrateState` 660–672 root-only) and **at-risk** for G1 (award loop 986–992 forces `IsAwarded = true` on non-null Award; see architecture Award-loop decision). `CalculateOnly` skip at 970–971 is correct.

### EventService
- **Path:** `Journeys.Core/Services/EventService.cs`
- **Responsibility:** `ProcessEventInternalAsync` — populate, campaigns, wrapper persist.
- **Depends on:** `LoyaltyAccountService`, `RulesService`.
- **Health:** **degraded (G2)**. After valid populate (867–872) loads campaigns and `ProcessCampaignsAsync` (924–929) with no bring-current. Reconcile **does** use bring-current (421, 582). MassTransit process-event publish commented out (~966–997) — do not revive. Skip bring-current when account invalid/not found; do not call on pre-save before the account exists.

### LoyaltyAccountService
- **Path:** `Journeys.Core/Services/LoyaltyAccountService.cs`
- **Responsibility:** Account + ledgers (~2.6k lines). `BringLoyaltyAccountPointsCurrentInternalAsync` expires `ExpirationDate <= UtcNow` via `ExpirePoints` (975–1012). `SaveLedgerExpirations` on move (2461–2486) mutates source expiration from current `ExpirationDate` (not `EarnDate`) and sets dest = `dest.PointsLifespanEndDate ?? UtcNow.AddDays(days ?? 100)`. Deposit-time expiration (~1482–1524) already uses `EarnDate + days`.
- **Health:** **at-risk** (god class + **G3** human-gated date math). Change only `SaveLedgerExpirations`. Do not redesign `PointLedgerTypeStrings`. No hosted sweep, no `/points/expire`.

### NotificationService
- **Path:** `Journeys.Core/Services/NotificationService.cs`
- **Implements:** `INotificationService`.
- **Responsibility:** ACTIVE config fetch + send through factory.
- **Health:** **at-risk**. Empty `configId` throws with `nameof(tenantId)` (60–61); adapter errors rethrown. Calculate must guard before calling. Works for manual/MassTransit send.

### PointAccountTypeValidator
- **Path:** `Journeys.Core/Services/PointAccountTypeValidator.cs`
- **Responsibility:** Lifespan / `expiresTo` pairing for rolling PATs.
- **Health:** **healthy**. 100-day move default can be removed without a validator change.

### ConfigureNotifications
- **Path:** `Journeys.Core/Configuration/ConfigureNotifications.cs`
- **Responsibility:** `AddNotificationsServices`.
- **Health:** **at-risk**. Registers `INotificationService` only when `DisableDataLake` is not true (19–23). Spec: always register.

## Notification ports and models

### INotificationService
- **Path:** `Journeys.Core/Interfaces/Services/INotificationService.cs`
- **Responsibility:** Config get + send port used by Core and consumers.
- **Health:** **healthy** as a port. Must be set on `RulesEngineState` for Award.

### INotificationAdapter
- **Path:** `Journeys.Core/Interfaces/Notifications/INotificationAdapter.cs`
- **Responsibility:** Adapter send port. Implemented by `RestApiAdapter`.
- **Health:** **healthy**.

### INotificationAdapterFactory
- **Path:** `Journeys.Core/Interfaces/Notifications/INotificationAdapterFactory.cs`
- **Responsibility:** Resolve adapter by type string.
- **Health:** **healthy**. Implementation supports `"rest_api"` only.

### INotificationConfigAdapter
- **Path:** `Journeys.Core/Interfaces/DataStorage/INotificationConfigAdapter.cs`
- **Responsibility:** Persist/load notification configs.
- **Health:** **healthy**. Implemented in DAL.

### NotificationConfig
- **Path:** `Journeys.Core/Models/NotificationConfig.cs`
- **Responsibility:** Domain config (adapter type, event-model filter, secrets).
- **Health:** **healthy**. `EventModelId` filter is a Calculate concern (mismatch → null).

### NotificationConfigDto
- **Path:** `Journeys.DTO/Models/NotificationConfigDto.cs`
- **Responsibility:** HTTP contract. `AdapterTypes` includes twilio/email/data_pipeline.
- **Health:** **healthy**. Enums stay; do not add adapters.

## Notification adapters

### RestApiAdapter
- **Path:** `Journeys.Notification/Adapters/RestApiAdapter.cs`
- **Implements:** `INotificationAdapter` (not `IRestApiAdapter`).
- **Responsibility:** HTTP POST; JSON-serializes any object.
- **Health:** **healthy**. No new adapter types this increment.

### NotificationAdapterFactory
- **Path:** `Journeys.Notification/Factories/NotificationAdapterFactory.cs`
- **Responsibility:** `"rest_api"` only.
- **Health:** **healthy** for the approved transport.

### RestApiConfig
- **Path:** `Journeys.Notification/Models/RestApiConfig.cs`
- **Responsibility:** Adapter configuration model.
- **Health:** **healthy**.

### IRestApiAdapter
- **Path:** `Journeys.Notification/Interfaces/IRestApiAdapter.cs`
- **Health:** **at-risk** (unused). Do not introduce a second send path.

### NotificationConfigAdapter
- **Path:** `Journeys.DAL/Adapters/NotificationConfigAdapter.cs`
- **Responsibility:** Backend persist for configs.
- **Health:** **healthy**.

## API host

### NotificationController
- **Path:** `Journeys.API/Controllers/NotificationController.cs`
- **Responsibility:** `api/Notification` CRUD (save / delete / get-by-status). No fire endpoint.
- **Health:** **healthy**. Thin controller.

### ConfigureDAL
- **Path:** `Journeys.API/Configuration/ConfigureDAL.cs`
- **Responsibility:** Always registers notification factory (and DAL adapters).
- **Health:** **healthy**. Pair with always-register `INotificationService`.

### Journeys.API Program
- **Path:** `Journeys.API/Program.cs`
- **Responsibility:** Host composition — REST, MCP, consumers, DI.
- **Health:** **healthy** for this increment.

### EventProcessedJobConsumer
- **Path:** `Journeys.API/Consumers/EventProcessedJobConsumer.cs`
- **Responsibility:** MassTransit → `SendNotificationsAsync` tenant-wide fan-out.
- **Health:** **healthy** as a sidecar path. Not a substitute for G1. Do not conflate with outcome Award.

### RulesEngineMcpContractSummary
- **Path:** `Journeys.API/Mcp/RulesEngineMcpContractSummary.cs`
- **Responsibility:** Authoring contract matrix.
- **Health:** **at-risk**. `MatrixVersion = "2026-06-20"`; Notification kind listed; **no** `notification_config_id` critical row. Closeout: add row + bump to `2026-09-18`; keep deposit/spend rows.

## Tests (intent-relevant, present)

### ExpirePointsOutcomeTests
- **Path:** `Journeys.Tests/RulesEngine/Outcomes/ExpirePointsOutcomeTests.cs`
- **Coverage:** Lifecycle balances (Escrow→Spendable→Expired). **No** dest `ExpirationDate` / `EarnDate+365` assertion.
- **Health:** **at-risk** until rewritten to earn-date expectations. Factory Spendable days are 30 today.

### RuleServiceTests
- **Path:** `Journeys.Tests/Services/RuleServiceTests.cs`
- **Coverage:** Root-campaign `HistoricalRule` TTL/decay via `TestCampaignFactory`.
- **Health:** **at-risk**. Long explicit constructors will break if `INotificationService` is added to the `RulesService` constructor — prefer state injection. Child-node hydrate tests absent.

### UserPointsTests
- **Path:** `Journeys.Tests/Services/UserPointsTests.cs`
- **Coverage:** Deposit/withdraw/expire via `LoyaltyAccountService`, **not** ProcessEvent order.
- **Health:** **healthy** for service math; does not prove G2.

### PointAccountTypeValidatorTests
- **Path:** `Journeys.Tests/Services/PointAccountTypeValidatorTests.cs`
- **Coverage:** Lifespan / `expiresTo` pairing.
- **Health:** **healthy**.

### RulesEngineMcpContractSummaryTests
- **Path:** `Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs`
- **Coverage:** Pins `MatrixVersion` `"2026-06-20"`.
- **Health:** **at-risk** — must bump with the matrix.

### TestDataFactory
- **Path:** `Journeys.Tests/TestDataFactory.cs`
- **Responsibility:** Shared fixtures. `GetSpendablePointAccount()` uses `PointsLifespanDays = 30`.
- **Health:** **at-risk** vs spec’s 365-day Spendable example. Tests must set 365 or assert EarnDate + configured days.

**Absent tests (do not invent as present):** `NotificationOutcomeTests`, `JourneyNodeCollectRulesTests`, child-node hydrate in `RuleServiceTests`, `LoyaltyAccountExpirationCascadeTests`, `EventServiceBringPointsCurrentTests`.

## Product / platform docs (deep-read)

### outcome.md
- **Path:** `docs/product/ontology/outcome.md`
- **Health:** **healthy vs code** — still states Notification Calculate/Award unimplemented. After ship, update in the same change (code canonical).

### loyalty-account.md
- **Path:** `docs/product/ontology/loyalty-account.md`
- **Health:** **at-risk** vs intended earn-date + ProcessEvent bring-current (docs unit).

### journey.md
- **Path:** `docs/product/ontology/journey.md`
- **Health:** **healthy** as language; hydrate walk belongs on `rule.md` / engine.

### rule.md
- **Path:** `docs/product/ontology/rule.md`
- **Health:** **at-risk** until it records tree-wide hydrate.

### event-model.md
- **Path:** `docs/product/ontology/event-model.md`
- **Health:** **healthy** wrapper/`*AndRuleState` contract.

### process-event.md
- **Path:** `docs/product/use-cases/process-event.md`
- **Health:** **at-risk**. Already claims notifications fire; code does not.

### path-map.yaml
- **Path:** `docs/product/graph/path-map.yaml`
- **Health:** **at-risk**. `Journeys.Notification` is `meaningOptional: true` — must become `false` in the docs/graph unit.

### docs/platform/architecture.md
- **Path:** `docs/platform/architecture.md`
- **Health:** **healthy**. Onion + DTO/Core/adapter/API rules for this increment.

### docs/platform/runtime.md
- **Path:** `docs/platform/runtime.md`
- **Health:** **at-risk** only if notification DI notes still describe the `DisableDataLake` gate after always-register.

### 2026-09-18-Journeys-mvp-engine-closeout-design.md
- **Path:** `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`
- **Health:** **healthy** (approved ingest). Not product canon; `docs/product/` + code win after ship.

### 2026-09-18-Journeys-mvp-engine-closeout.md
- **Path:** `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md`
- **Health:** **healthy** (approved ingest). Units-generation should stay close to its five tasks.

## Skimmed packages (not inventoried as deep components)

`Journeys.Infra*`, `Journeys.Agent`, `Journeys.CampaignAgent.Remediation`, `Journeys.UX`, remaining controllers/services/DTO folders — see timestamp `shallow.paths`.
