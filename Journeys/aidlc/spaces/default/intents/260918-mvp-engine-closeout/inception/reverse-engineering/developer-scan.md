# Developer Code Scan — mvp-engine-closeout

Ingested (not redesigned): `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`, `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md`. Product slice: `AGENTS.md`, `docs/product/ontology/{outcome,loyalty-account,journey,rule,event-model}.md`, `docs/platform/architecture.md`. Snapshot: `./` (NO_STORE, first full scan). Code is canonical when docs disagree. No Journeys.* product code written.

## Developer Code Scan Results

### Scan Coverage
- **Analyzed deeply**:
  - Journeys.Core/RulesEngine/Journey/JourneyNode.cs
  - Journeys.Core/RulesEngine/Journey/SimpleNavigationCriteria.cs
  - Journeys.Core/RulesEngine/Journey/JourneyNavigator.cs
  - Journeys.Core/RulesEngine/Engine/RulesEngineState.cs
  - Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs
  - Journeys.Core/RulesEngine/Outcomes/OutcomeBase.cs
  - Journeys.Core/RulesEngine/Outcomes/TagOutcome.cs
  - Journeys.Core/RulesEngine/Outcomes/DepositPointsOutcome.cs
  - Journeys.Core/RulesEngine/Outcomes/WorkflowOutcome.cs
  - Journeys.Core/RulesEngine/Outcomes/RuleStateOutcome.cs
  - Journeys.Core/RulesEngine/Outcomes/ThirdPartyOutcome.cs
  - Journeys.Core/RulesEngine/Rules/HistoricalRule.cs
  - Journeys.Core/RulesEngine/Rules/RuleBase.cs
  - Journeys.Core/RulesEngine/RuleSet.cs
  - Journeys.Core/RulesEngine/Providers/PointBalanceProvider.cs
  - Journeys.Core/Services/RulesService.cs
  - Journeys.Core/Services/EventService.cs
  - Journeys.Core/Services/LoyaltyAccountService.cs
  - Journeys.Core/Services/NotificationService.cs
  - Journeys.Core/Services/PointAccountTypeValidator.cs
  - Journeys.Core/Configuration/ConfigureNotifications.cs
  - Journeys.Core/Interfaces/Services/INotificationService.cs
  - Journeys.Core/Interfaces/Notifications/INotificationAdapter.cs
  - Journeys.Core/Interfaces/Notifications/INotificationAdapterFactory.cs
  - Journeys.Core/Interfaces/DataStorage/INotificationConfigAdapter.cs
  - Journeys.Core/Models/NotificationConfig.cs
  - Journeys.DTO/Models/NotificationConfigDto.cs
  - Journeys.Notification/Adapters/RestApiAdapter.cs
  - Journeys.Notification/Factories/NotificationAdapterFactory.cs
  - Journeys.Notification/Models/RestApiConfig.cs
  - Journeys.Notification/Interfaces/IRestApiAdapter.cs
  - Journeys.Notification/Journeys.Notification.csproj
  - Journeys.API/Mcp/RulesEngineMcpContractSummary.cs
  - Journeys.API/Controllers/NotificationController.cs
  - Journeys.API/Configuration/ConfigureDAL.cs
  - Journeys.API/Program.cs
  - Journeys.API/Consumers/EventProcessedJobConsumer.cs
  - Journeys.DAL/Adapters/NotificationConfigAdapter.cs
  - Journeys.Tests/RulesEngine/Outcomes/ExpirePointsOutcomeTests.cs
  - Journeys.Tests/Services/RuleServiceTests.cs
  - Journeys.Tests/Services/UserPointsTests.cs
  - Journeys.Tests/Services/PointAccountTypeValidatorTests.cs
  - Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs
  - Journeys.Tests/TestDataFactory.cs
  - docs/product/ontology/outcome.md
  - docs/product/ontology/loyalty-account.md
  - docs/product/ontology/journey.md
  - docs/product/ontology/rule.md
  - docs/product/ontology/event-model.md
  - docs/product/use-cases/process-event.md
  - docs/product/graph/path-map.yaml
  - docs/platform/architecture.md
  - docs/platform/runtime.md
  - docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md
  - docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md
- **Skimmed only**:
  - Journeys.Core/RulesEngine/Comparitors/
  - Journeys.Core/RulesEngine/Providers/ (except PointBalanceProvider)
  - Journeys.Core/RulesEngine/Journey/ (except JourneyNode, SimpleNavigationCriteria, JourneyNavigator)
  - Journeys.Core/Services/ (except EventService, RulesService, LoyaltyAccountService, NotificationService, PointAccountTypeValidator)
  - Journeys.Core/Services/Ingest/
  - Journeys.Core/Models/ (except NotificationConfig)
  - Journeys.Core/Interfaces/ (except notification + INotificationService)
  - Journeys.DTO/Requests/
  - Journeys.DTO/Responses/
  - Journeys.DTO/Models/ (except NotificationConfigDto)
  - Journeys.API/Controllers/ (except NotificationController)
  - Journeys.API/Mcp/ (except RulesEngineMcpContractSummary)
  - Journeys.API/CampaignAgent/
  - Journeys.API/Consumers/ (except EventProcessedJobConsumer)
  - Journeys.DAL/ (except NotificationConfigAdapter + csproj)
  - Journeys.Infra/
  - Journeys.Infra.Auth/
  - Journeys.Infra.Backend/
  - Journeys.Infra.Llm/
  - Journeys.Agent/
  - Journeys.CampaignAgent.Remediation/
  - Journeys.UX/
  - Journeys.Tests/CampaignAgent/
  - Journeys.Tests/Workflow/
  - Journeys.Tests/Controllers/
  - Journeys.Tests/Infra/
  - Journeys.Tests/Utility/
  - Journeys.Tests/Stubs/
  - Journeys.Tests/RulesEngine/ (except ExpirePointsOutcomeTests)
  - docs/product/graph/ (except path-map.yaml)
  - docs/developer/
  - docs/roadmap/
  - scripts/
  - aidlc/
  - Model/ Schema/ Temp/ tools (outside this folder)

### Packages Found
- Journeys.DTO — class library — C# net8.0 — API/controller contracts; no business rules
- Journeys.Core — class library — C# net8.0 — domain services and rules engine
- Journeys.DAL — class library — C# net8.0 — persistence adapters (Backend data plane)
- Journeys.Notification — class library — C# net8.0 — notification adapters only (`rest_api`)
- Journeys.Infra.BlobStorage / DataLake / ServiceBus — class libraries — C# net8.0 — Azure adapters
- Journeys.Infra.Auth — class library — C# net8.0 — Auth0 / JWT / API keys
- Journeys.Infra.Backend — class library — C# net8.0 — Backend.* client adapters
- Journeys.Infra.Llm — class library — C# net8.0 — OpenAI-compatible / Ollama chat clients
- Journeys.API — ASP.NET Core web — C# net8.0 — REST, MCP, campaign-agent HTTP host
- Journeys.Agent — console exe — C# net8.0 — campaign authoring host
- Journeys.CampaignAgent.Remediation — class library — C# net8.0 — agent remediation catalog
- Journeys.Tests — xUnit test project — C# net8.0 — unit/service tests
- Journeys.UX — npm private app — TypeScript / Next.js 16 — HTTP-only UI (out of this increment)
- Backend.Core / Backend.Dto / Backend.Llm.Anthropic — external HintPath DLLs — `..\..\Binaries\`

### Build System
- **Type**: dotnet / MSBuild (SDK-style csproj) + npm for Journeys.UX
- **Config Files**: per-project `*.csproj` (`TargetFramework` net8.0); `Journeys.UX/package.json`; no `Directory.Build.props` observed at scan time; solution referenced as `Journeys.sln` in `AGENTS.md`
- **Build Dependencies**:
  - Journeys.DTO → Backend.Dto (HintPath)
  - Journeys.Core → Journeys.DTO, Journeys.CampaignAgent.Remediation, Backend.Dto
  - Journeys.DAL → Journeys.Core, Backend.Dto
  - Journeys.Notification → Journeys.Core
  - Journeys.API → Core, DTO, DAL, Notification, Infra.*, CampaignAgent.Remediation, Backend.Core/Dto/Llm.Anthropic
  - Journeys.Tests → API, Core, DAL, Infra.*, CampaignAgent.Remediation, Backend.Core/Dto (does **not** reference Journeys.Notification)
  - Journeys.Agent → CampaignAgent.Remediation only (HTTP/MCP client, not Core)

### APIs Discovered
- REST `NotificationController` (`api/Notification`) — 3 actions: POST `{tenantId}/save`, DELETE `{tenantId}/{configId}`, GET `{tenantId}/{status}`. No send/fire endpoint.
- REST process-event — `EventsController` + `IEventService.ProcessEventAsync` (skimmed controller; deep-read `EventService.ProcessEventInternalAsync`)
- MCP contract surface — `RulesEngineMcpContractSummary.Build()`; `MatrixVersion = "2026-06-20"`; `NotificationOutcome` listed in `OutcomeKinds` but **no** `NotificationConfigId` critical row
- MassTransit job consumers — `EventProcessedJobConsumer` / `PointsChangedJobConsumer` call `INotificationService.SendNotificationsAsync` (tenant-wide fan-out). `EventService` MassTransit process-event publish is **commented out** (lines ~966–997)
- Internal outcome API — `OutcomeBase.CalculateOutcomeAsync(RulesEngineState, CancellationToken)` then `AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)` (signature must not change)

### Frameworks & Libraries
- .NET 8 / ASP.NET Core — host + onion projects
- xUnit 2.9.3 + Microsoft.NET.Test.Sdk 18.0.0 + coverlet.collector 6.0.3 — Journeys.Tests
- MassTransit 8.5.5 — Core + API consumers (do not revive process-event publish this increment)
- Microsoft.Extensions.* 9.0.10 (Core/DAL/Notification); Auth JWT 8.0.15
- Swashbuckle 6.5.0, Application Insights 2.22.0, ModelContextProtocol 1.1.0 — API
- Serilog 4.3.0 + Azure Analytics sinks — logging
- Microsoft.EntityFrameworkCore 9.0.10 — referenced by Core (unused import on `RulesEngineState`)
- Next.js ^16.0.7, React 19, Vitest 3 — Journeys.UX (out of increment)
- Backend.* HintPath assemblies — external, not renamed

### Test Coverage
- **Test Directories**: `Journeys.Tests/` (RulesEngine/Outcomes, Services, Mcp, Workflow, CampaignAgent, Controllers, Utility, Stubs, Infra); `Journeys.UX` vitest (out of increment)
- **Test Frameworks**: xUnit; Vitest (UX only)
- **Coverage Config**: coverlet.collector present; no coverlet.runsettings / coverage floor file found
- **Intent-relevant tests present**: `ExpirePointsOutcomeTests` (lifecycle balances, **no dest ExpirationDate / EarnDate+365 assertion**); `RuleServiceTests` (root-campaign HistoricalRule TTL/decay via `TestCampaignFactory`); `UserPointsTests` (deposit/withdraw/expire via LoyaltyAccountService, **not** ProcessEvent order); `PointAccountTypeValidatorTests` (lifespan/`expiresTo` pairing); `RulesEngineMcpContractSummaryTests` (pins `MatrixVersion` `"2026-06-20"`)
- **Intent-relevant tests absent**: `NotificationOutcomeTests`, `JourneyNodeCollectRulesTests`, child-node hydrate in `RuleServiceTests`, `LoyaltyAccountExpirationCascadeTests`, `EventServiceBringPointsCurrentTests`

### Code Quality Indicators
- **Linting**: no `.editorconfig` observed at scan; C# follows `docs/platform/coding-standards.md` / nullable enable; UX uses TypeScript
- **CI/CD**: no `.github/` workflows in this tree; local gate is `scripts/agent-verify.ps1` / `scripts/aidlc-agent-verify-sensor.ps1` plus `docs-impact` / `graph-impact`
- **Documentation**: product ontology + use-case + platform runtime present; ontology still states Notification Calculate/Award unimplemented (matches code); process-event use case still claims notifications fire; `path-map.yaml` `Journeys.Notification` is `meaningOptional: true`

### Technical Debt Signals
- `NotificationOutcome.CalculateOutcomeAsync` / `AwardOutcomeAsync` are `//TODO: Implement` and `return null` (`NotificationOutcome.cs` 21–31). Same stub pattern on `WorkflowOutcome` / `RuleStateOutcome` (out of increment; do not implement).
- `ThirdPartyOutcome` is **not** an `OutcomeBase` — do not promote.
- `ConfigureNotifications.AddNotificationsServices` registers `INotificationService` only when `DisableDataLake` is not true (`ConfigureNotifications.cs` 19–23). Factory is always registered in `ConfigureDAL`.
- `RulesEngineState` has `LoyaltyAccountService` but **no** `NotificationService`. `RulesService` constructor does not take `INotificationService`. `NavigatePayload` copies `LoyaltyAccountService` only (248–249).
- `RulesService.ProcessJourneyAsync` award loop (`~986–992`) sets `earnedOutcome.IsAwarded = true` whenever Award returns non-null — conflicts with spec “webhook fail → IsAwarded false” if the award loop stays unchanged.
- `JourneyNode.FlattenToRulesOfType<T>` walks `NavConstraint` via `OfType<T>()` (no composite flatten) + `Children`; it does **not** walk `Rules` (`JourneyNode.cs` 234–257).
- `HydrateState` collects Historical/Taxonomic rules only from `campaign.Journey.Rules` root RuleSets (`RulesService.cs` 660–672). Child earn RuleSets and nav trees are skipped.
- `SaveLedgerExpirations` on move sets dest `ExpirationDate = dest.PointsLifespanEndDate ?? UtcNow.AddDays(days ?? 100)` (`LoyaltyAccountService.cs` 2484–2486). Earlier lines 2461–2466 also mutate the source entry’s expiration from **current ExpirationDate**, not `EarnDate`. `EarnDate` is not overwritten (same `LedgerEntry` object is moved).
- `ProcessEventInternalAsync` populates the account then calls `ProcessCampaignsAsync` with **no** `BringLoyaltyAccountPointsCurrentInternalAsync` (`EventService.cs` 867–929). Bring-current **is** used on reconcile paths (`421`, `582`).
- `PointBalanceProvider` reads `LoyaltyAccount.PointLedgers` and, if null, loads via `GetLoyaltyAccountPointsAsync` **without** resettle/expire (`PointBalanceProvider.cs` 30–36).
- `INotificationService.GetNotificationConfigAsync` fetches **ACTIVE** only; empty `configId` throws `ArgumentNullException` with `nameof(tenantId)` (`NotificationService.cs` 60–61). Adapter exceptions are rethrown (not mapped to null).
- `NotificationAdapterFactory` supports only `"rest_api"`. DTO `AdapterTypes` still lists twilio/email/data_pipeline (enums stay; do not add adapters).
- `IRestApiAdapter` is unused; `RestApiAdapter` implements `INotificationAdapter`.
- `EventService` MassTransit process-event publish is commented out; do not revive.
- `LoyaltyAccountService` is a large multi-responsibility type (~2.6k lines). Ledger date math is human-gated.
- `RulesEngineState` unused usings: `Microsoft.Azure.Amqp.Framing`, `Microsoft.EntityFrameworkCore.ValueGeneration.Internal`.
- `TestDataFactory.GetSpendablePointAccount()` uses `PointsLifespanDays = 30`, not the spec’s 365-day Spendable example.
- MCP tests and workflow pin artifacts hard-code `MatrixVersion` `"2026-06-20"`.
- Docs drift: `outcome.md` matches unimplemented Notification; `process-event.md` already claims notifications fire; `path-map.yaml` marks Notification meaning-optional.

### Intent-slice findings (four approved gaps, current code)

**G1 — NotificationOutcome never fires**

- Kind exists (`OutcomeKindDiscriminators.NotificationOutcome`). Calculate/Award return null, so `JourneyNode.ProcessAsync` never adds an `OutcomeResult` and Award never runs.
- Tenant webhook CRUD + `RestApiAdapter` exist and work for **manual / MassTransit** send (`SendNotificationAsync(tenantId, configId, payload)` JSON-serializes any object).
- Binding property `NotificationConfigId` does not exist on the outcome. Closed DTO `NotificationOutcomePayload` does not exist.
- Wiring required by spec: put `INotificationService` on `RulesEngineState`; `RulesService` sets it when constructing state; do **not** change `AwardOutcomeAsync` signature.
- CalculateOnly already skips the award loop (`RulesService.cs` 970–971). Missing/inactive config can return null from Calculate without throwing **if** Calculate checks id before `GetNotificationConfigAsync`.
- MCP: add critical row `notification_config_id` / `TIER_A_NOTIFICATION_MISSING_CONFIG_ID`; bump `MatrixVersion` to `2026-09-18`. Existing deposit/spend rows must stay.

**G2 — Expire-on-process missing**

- After a valid populate (`EventService.cs` 867–872) the method loads campaigns and calls `ProcessCampaignsAsync` (`924–929`) with no bring-current.
- `BringLoyaltyAccountPointsCurrentInternalAsync` already expires entries with `ExpirationDate <= UtcNow` via `ExpirePoints` (`LoyaltyAccountService.cs` 975–1012). Reconcile already assigns `PointLedgers` from it.
- Spec placement: after populate, before campaigns; skip when account invalid/not found; include draft verification; do not call on pre-save before the account exists.

**G3 — Cascade clock resets on move (human-gated ledger)**

- On hop, dest expiration is `UtcNow + (PointsLifespanDays ?? 100)`, not `EarnDate + dest.days` (`SaveLedgerExpirations` 2484–2486).
- `PointAccountTypeValidator` already requires lifespan/`expiresTo` pairing for rolling PATs — the 100-day default is removable without a validator change.
- `ExpirePointsOutcomeTests.PointLifecycle_EscrowToSpendableToExpired` asserts balances only; it does **not** lock dest `ExpirationDate`. Spec requires rewriting lifecycle expectations to earn-date (Spendable expire = EarnDate+365 in the stated program). Factory Spendable days are 30 today — tests must set 365 (or assert EarnDate+configured days).
- Do **not** redesign `PointLedgerTypeStrings`. Deposit-time expiration (`~1482–1524`) already uses `EarnDate + days` when writing **new** deposits; only the **move** path is the closeout change.

**G4 — Hydrate is root-RuleSet only**

- `HydrateState` (`RulesService.cs` 660–672) uses `campaign.Journey.Rules` + `RuleSet.FlattenToRulesOfType<T>` (that helper **does** flatten composites).
- Child-node earn RuleSets and `NavConstraint` trees are not collected. `JourneyNode.FlattenToRulesOfType` is the wrong helper as-is: it skips `Rules` and does not flatten composite nav constraints.
- Plan: add `CollectEarnAndNavRulesOfType<T>` walking Rules (each RuleSet), nav constraints (flatten composites — do not silently drop nested HistoricalRule), then Children. Point `HydrateState` at it. Keep TTL/decay/`LoyaltyAccount.RuleState` load/`finally` upsert.
- Existing `RuleServiceTests` HistoricalCampaign fixtures place HistoricalRule on **root** RuleSets, so they will not catch the bug.

## Handoff Summary
- **Intent-relevant finding**: All four approved closeout gaps are present and localized in current Core. `NotificationOutcome` Calculate/Award return null (`Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs` 21–31) while tenant `rest_api` send already exists; `HydrateState` reads only `campaign.Journey.Rules` (`RulesService.cs` 660–672); `ProcessEventInternalAsync` never calls `BringLoyaltyAccountPointsCurrentInternalAsync` after populate (`EventService.cs` 867–929); `SaveLedgerExpirations` sets dest expiration from `UtcNow + (days ?? 100)` (`LoyaltyAccountService.cs` 2484–2486). Ontology `outcome.md` matches the unimplemented webhook; code remains canonical.
- **Risks / follow-up**:
  1. Award loop (`RulesService.cs` ~986–992) forces `IsAwarded = true` on any non-null Award result. Spec wants webhook failure `IsAwarded = false` **and** an unchanged `AwardOutcomeAsync` signature / unchanged award loop. Architect must pick: return null on send failure (loop skips), or adjust the loop, or accept overwritten `IsAwarded`.
  2. Adding `INotificationService` to the `RulesService` constructor breaks the long explicit constructors in `RuleServiceTests` (and similar). Prefer the same injection style as `loyaltyAccountService` already on state.
  3. `NavigatePayload` must copy `NotificationService` if it is added to `RulesEngineState`.
  4. `GetNotificationConfigAsync` throws on empty id / adapter errors and only loads ACTIVE. Calculate must guard `NotificationConfigId` and treat missing/inactive as null (do not fail the event).
  5. Ledger unit is human-gated. Change only `SaveLedgerExpirations` date math; do not redesign `PointLedgerTypeStrings` or add a hosted sweep / `/points/expire`.
  6. `CollectEarnAndNavRulesOfType` must flatten composite `NavConstraint` trees; `OfType<T>` on the constraint root drops nested `HistoricalRule`.
  7. Do not revive MassTransit process-event publish in `EventService`. Do not add email/Twilio/data_pipeline adapters. Do not implement `WorkflowOutcome` / `RuleStateOutcome` or promote `ThirdPartyOutcome`.
  8. MCP `MatrixVersion` is pinned at `"2026-06-20"` in `RulesEngineMcpContractSummaryTests` and workflow pin artifacts — bump tests with the matrix.
  9. `Journeys.UX` is out of increment. `Journeys.Notification` path-map `meaningOptional: true` must become `false` in the docs/graph unit.
  10. `DisableDataLake` currently gates `INotificationService` registration; spec requires always-register so Award can resolve the service when Data Lake is off.
