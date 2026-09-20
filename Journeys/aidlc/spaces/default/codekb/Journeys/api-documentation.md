# API Documentation

Surfaces discovered by the developer scan. Controllers stay thin; contracts live in `Journeys.DTO`. All business operations are tenant-scoped.

## External REST — notifications

`NotificationController` route prefix `api/Notification`. **No send/fire endpoint** on this controller.

| Method | Template | Behavior |
|---|---|---|
| POST | `{tenantId}/save` | Upsert tenant `NotificationConfig` |
| DELETE | `{tenantId}/{configId}` | Delete config |
| GET | `{tenantId}/{status}` | List by status |

Configs with `AdapterType = rest_api` are the webhook source of truth. Operator creates the config before a campaign can bind `NotificationConfigId` (property does not exist on the outcome yet).

## External REST — process-event

`EventsController` (skimmed) + `IEventService.ProcessEventAsync`. Deep-read implementation: `EventService.ProcessEventInternalAsync`.

- Populates the loyalty account, then `ProcessCampaignsAsync`.
- **Does not** call `BringLoyaltyAccountPointsCurrentInternalAsync` on this path (G2).
- MassTransit process-event publish in `EventService` is commented out (~966–997). Do not revive.
- Event process returns Backend `validationErrors` on `EventPayloadResponseDto.Errors`.

## MCP contract surface

`RulesEngineMcpContractSummary.Build()`:

- `MatrixVersion = "2026-06-20"` (pinned by `RulesEngineMcpContractSummaryTests` and workflow pin artifacts).
- `NotificationOutcome` is listed in `OutcomeKinds`.
- **No** critical row for `NotificationConfigId` / `TIER_A_NOTIFICATION_MISSING_CONFIG_ID`.
- Existing deposit/spend critical rows must stay when the matrix bumps to `2026-09-18`.

Authoring already upserts campaigns (including outcomes) and notification configs via Agent/REST. No UX this increment.

## MassTransit consumers

| Consumer | Call | Notes |
|---|---|---|
| `EventProcessedJobConsumer` | `INotificationService.SendNotificationsAsync` | Tenant-wide fan-out, not outcome-bound |
| `PointsChangedJobConsumer` | same | Same fan-out pattern |

This is **not** the G1 webhook-on-outcome path.

## Internal outcome API (must not change signature)

```
OutcomeBase.CalculateOutcomeAsync(RulesEngineState, CancellationToken)
OutcomeBase.AwardOutcomeAsync(RulesEngineState, ILoyaltyAccountService, CancellationToken)
```

- Calculate sizes an `OutcomeResult` (`IsAwarded = false`) or returns null.
- Award writes ledgers/tags (and, once implemented, sends the webhook).
- `CalculateOnly` skips the award loop (`RulesService` 970–971).
- `NotificationOutcome` Calculate/Award are `//TODO: Implement` and `return null` (21–31).
- `WorkflowOutcome` / `RuleStateOutcome` use the same stub pattern — **out of increment**.
- `ThirdPartyOutcome` is **not** an `OutcomeBase` — do not promote.

Closed DTO `NotificationOutcomePayload` **does not exist** yet. Spec fields (strings/dates, no event body, no ledger rows, no secrets): `tenantId`, `loyaltyAccountId`, `extAccountId`, `campaignId`, `ruleSetId`, `issuingOutcomeId`, `issuingOutcomeKind`, `eventId`, `eventType`, `eventModelId`, `awardedAtUtc`. `RestApiAdapter` already JSON-serializes any object.

## Internal notification service API

`INotificationService` (implemented by `NotificationService`):

- `GetNotificationConfigAsync` — **ACTIVE** only. Empty `configId` throws `ArgumentNullException` with `nameof(tenantId)` (60–61). Adapter exceptions are rethrown (not mapped to null). Calculate must guard id and treat missing/inactive as null (do not fail the event).
- `SendNotificationAsync(tenantId, configId, payload)` — JSON-serializes any object through the factory.
- `SendNotificationsAsync` — tenant-wide send used by consumers.

`NotificationAdapterFactory` supports only `"rest_api"`. `IRestApiAdapter` is unused.

## DI / host composition

- `ConfigureNotifications.AddNotificationsServices` registers `INotificationService` only when `DisableDataLake` is **not** true (19–23). Spec: always register.
- Factory is always registered in `ConfigureDAL`.
- `Journeys.API/Program.cs` composes onion + consumers.

## Cross-reference

Sequence of process-event vs webhook: [architecture.md](architecture.md) Interaction Diagrams. Types: [component-inventory.md](component-inventory.md).
