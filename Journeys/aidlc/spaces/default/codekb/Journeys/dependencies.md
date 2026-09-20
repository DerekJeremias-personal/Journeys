# Dependencies

## Internal project references

Inward onion. Controllers do not take Core business types from DAL. UX has no C# project reference.

```
Journeys.DTO → Backend.Dto (HintPath)
Journeys.Core → Journeys.DTO, Journeys.CampaignAgent.Remediation, Backend.Dto
Journeys.DAL → Journeys.Core, Backend.Dto
Journeys.Notification → Journeys.Core
Journeys.API → Core, DTO, DAL, Notification, Infra.*, CampaignAgent.Remediation, Backend.Core / Dto / Llm.Anthropic
Journeys.Tests → API, Core, DAL, Infra.*, CampaignAgent.Remediation, Backend.Core / Dto
Journeys.Agent → CampaignAgent.Remediation only (HTTP/MCP client, not Core)
```

**Scan note:** `Journeys.Tests` does **not** reference `Journeys.Notification`. NotificationOutcome tests should fake `INotificationService` on `RulesEngineState` (Core port), not construct `RestApiAdapter`.

## Cross-package runtime edges (intent slice)

| From | To | Why |
|---|---|---|
| `RulesService` | `RulesEngineState`, `JourneyNode`, `OutcomeBase` | Hydrate, navigate, calculate/award |
| `EventService` | `LoyaltyAccountService`, `RulesService` | Populate, (missing) bring-current, process campaigns |
| `NotificationOutcome` (intended) | `RulesEngineState.NotificationService` | Load config + send; **not wired** |
| `NotificationService` | `INotificationAdapterFactory`, `INotificationConfigAdapter` | ACTIVE config + adapter send |
| `NotificationAdapterFactory` | `RestApiAdapter` | `"rest_api"` only |
| `NotificationConfigAdapter` | Backend data plane | Persist configs |
| `EventProcessedJobConsumer` | `INotificationService` | Tenant-wide fan-out |
| `PointBalanceProvider` | `LoyaltyAccount.PointLedgers` / `GetLoyaltyAccountPointsAsync` | Balance read **without** resettle/expire |
| `PointAccountTypeValidator` | PAT lifespan / `expiresTo` pairing | Already required for rolling PATs |

## External systems (adapters only)

| System | Package | Constraint |
|---|---|---|
| Backend.* data plane | DAL + HintPath DLLs | Persistence; wrapper lowercase JSON |
| Azure Blob / Data Lake / Service Bus | `Journeys.Infra*` | No Core Azure SDK |
| Auth0 / JWT / API keys | `Journeys.Infra.Auth` | Auth0 tenant `"hayward"` is human-gated; do not edit |
| OpenAI-compatible / Ollama | `Journeys.Infra.Llm` | Campaign Agent; out of this increment |
| Tenant webhook HTTP | `Journeys.Notification` / `RestApiAdapter` | Secrets stay on `NotificationConfig`; do not log auth headers |

`DisableDataLake` currently skips `INotificationService` registration (`ConfigureNotifications` 19–23) even though the factory is always registered. Award cannot resolve the service when Data Lake is off until that gate is removed.

## NuGet / framework versions

Recorded once in [technology-stack.md](technology-stack.md). Do not duplicate the version table here.

## Forbidden dependency moves (this intent)

- Do not project-reference Core/DAL/Infra from `Journeys.UX`.
- Do not add email/Twilio/data_pipeline adapter packages.
- Do not add a hosted sweep host or `/points/expire` endpoint.
- Do not take a new `Journeys.Tests` → `Journeys.Notification` reference unless a later unit proves it necessary (prefer the Core port fake).
