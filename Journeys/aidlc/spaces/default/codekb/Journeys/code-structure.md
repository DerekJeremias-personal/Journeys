# Code Structure

## Package / module organization

Solution is `Journeys.sln` (referenced from `AGENTS.md`). SDK-style csproj, `TargetFramework` net8.0. No `Directory.Build.props` observed at scan time. UX is a sibling npm app, not a C# project.

| Package | Type | Purpose |
|---|---|---|
| `Journeys.DTO` | class library | Controller/API contracts only; no business rules |
| `Journeys.Core` | class library | Domain services and rules engine |
| `Journeys.DAL` | class library | Persistence adapters (Backend data plane) |
| `Journeys.Notification` | class library | Notification adapters only (`rest_api`) |
| `Journeys.Infra.BlobStorage` / `DataLake` / `ServiceBus` | class libraries | Azure adapters |
| `Journeys.Infra.Auth` | class library | Auth0 / JWT / API keys |
| `Journeys.Infra.Backend` | class library | Backend.* client adapters |
| `Journeys.Infra.Llm` | class library | OpenAI-compatible / Ollama chat clients |
| `Journeys.API` | ASP.NET Core web | REST, MCP, campaign-agent HTTP host |
| `Journeys.Agent` | console exe | Campaign authoring host |
| `Journeys.CampaignAgent.Remediation` | class library | Agent remediation catalog |
| `Journeys.Tests` | xUnit | Unit/service tests |
| `Journeys.UX` | npm private | Next.js 16 HTTP-only UI — **out of increment** |
| `Backend.Core` / `Backend.Dto` / `Backend.Llm.Anthropic` | HintPath DLLs | `..\..\Binaries\` — external, not renamed |

Layer arrow (center → edge): `Journeys.DTO` → `Journeys.Core` → `Journeys.DAL` / `Journeys.Infra*` / `Journeys.Notification` → `Journeys.API` / `Journeys.Agent`.

## Intent-slice file map (deep-read)

Closeout placement from the ingested spec, confirmed present by the scan:

```
Journeys.Core/RulesEngine/Journey/JourneyNode.cs
Journeys.Core/RulesEngine/Journey/SimpleNavigationCriteria.cs
Journeys.Core/RulesEngine/Journey/JourneyNavigator.cs
Journeys.Core/RulesEngine/Engine/RulesEngineState.cs
Journeys.Core/RulesEngine/Outcomes/*.cs
Journeys.Core/RulesEngine/Rules/{RuleBase,HistoricalRule}.cs
Journeys.Core/RulesEngine/RuleSet.cs
Journeys.Core/RulesEngine/Providers/PointBalanceProvider.cs
Journeys.Core/Services/{RulesService,EventService,LoyaltyAccountService,NotificationService,PointAccountTypeValidator}.cs
Journeys.Core/Configuration/ConfigureNotifications.cs
Journeys.Core/Interfaces/Services/INotificationService.cs
Journeys.Core/Interfaces/Notifications/*
Journeys.Core/Models/NotificationConfig.cs
Journeys.DTO/Models/NotificationConfigDto.cs
Journeys.Notification/Adapters/RestApiAdapter.cs
Journeys.Notification/Factories/NotificationAdapterFactory.cs
Journeys.API/Mcp/RulesEngineMcpContractSummary.cs
Journeys.API/Controllers/NotificationController.cs
Journeys.API/Configuration/ConfigureDAL.cs
Journeys.API/Program.cs
Journeys.API/Consumers/EventProcessedJobConsumer.cs
Journeys.DAL/Adapters/NotificationConfigAdapter.cs
Journeys.Tests/RulesEngine/Outcomes/ExpirePointsOutcomeTests.cs
Journeys.Tests/Services/{RuleServiceTests,UserPointsTests,PointAccountTypeValidatorTests}.cs
Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs
Journeys.Tests/TestDataFactory.cs
```

Planned adds (not present): `Journeys.DTO/Models/NotificationOutcomePayload.cs`; `Journeys.Tests/RulesEngine/Journey/JourneyNodeCollectRulesTests.cs`; `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs`; `Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs`; `Journeys.Tests/Services/EventServiceBringPointsCurrentTests.cs`.

## File classification

| Kind | Location | Pattern |
|---|---|---|
| Domain services | `Journeys.Core/Services/` | `*Service`, constructor injection, `...Async` |
| Rules engine | `Journeys.Core/RulesEngine/` | Journey / Engine / Outcomes / Rules / Providers / Comparitors |
| Ports | `Journeys.Core/Interfaces/` | `I*` consumed by Core, implemented in adapters |
| DTOs | `Journeys.DTO/{Models,Requests,Responses}/` | HTTP/MCP contracts |
| Persistence adapters | `Journeys.DAL/Adapters/` | Backend data plane |
| Notification adapters | `Journeys.Notification/Adapters/` | `INotificationAdapter` |
| Host composition | `Journeys.API/Program.cs`, `Configuration/` | DI, MassTransit consumers |
| MCP | `Journeys.API/Mcp/` | Contract summary + tools |
| Tests | `Journeys.Tests/` | Mirror area folders (RulesEngine, Services, Mcp, …) |
| Product meaning | `docs/product/` | Ontology, use cases, graph YAML |
| Platform | `docs/platform/` | Onion, runtime, overlays |

## Code patterns

- File-scoped namespaces, nullable enable, constructor injection, readonly `_camelCase` (platform coding standards).
- Outcomes: `Kind` discriminators (`OutcomeKindDiscriminators`), not `$type`. Nested providers still use `$type`.
- Two-phase outcomes: `CalculateOutcomeAsync` then `AwardOutcomeAsync`. `CalculateOnly` short-circuits award.
- Journey flatten today: `RuleSet.FlattenToRulesOfType<T>` **does** flatten composites. `JourneyNode.FlattenToRulesOfType<T>` walks `NavConstraint` via `OfType<T>()` (no composite flatten) + `Children`, and **does not** walk `Rules` (234–257).
- State bag: `RulesEngineState` carries `LoyaltyAccountService` and is copied through `NavigatePayload` (248–249). No `NotificationService` property.
- Notification factory: `"rest_api"` only. DTO `AdapterTypes` still lists twilio/email/data_pipeline — enums stay; do not add adapters.
- `IRestApiAdapter` is unused; `RestApiAdapter` implements `INotificationAdapter`.
- Persist JSON for wrapper payloads uses lowercase Backend symbols (`appliedcampaigns`, `outcomestates`, …) — see platform architecture; do not “fix” to camelCase on persist.
- Tests pin MCP `MatrixVersion` `"2026-06-20"`; `TestDataFactory.GetSpendablePointAccount()` uses `PointsLifespanDays = 30` (spec example is 365).

## Skimmed areas (not re-inventoried here)

See [reverse-engineering-timestamp.md](reverse-engineering-timestamp.md) `shallow.paths`. Notable skims: remaining Core services/models, DTO request/response types, most API controllers, Infra*, Agent, CampaignAgent, UX, most Tests, scripts, aidlc.

## Cross-reference

Per-type responsibilities: [component-inventory.md](component-inventory.md). Versions: [technology-stack.md](technology-stack.md).
