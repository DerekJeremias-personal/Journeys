# Code summary — `u4-notification-webhook`

## Files created or modified

- `Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs` — eight Calculate/Award cases plus DI companion: closed payload, missing id, missing/inactive/mismatch/foreign tenant, send false + sibling kept, send throw fail-closed, throw-as-false, CalculateOnly, honor IsAwarded / Award signature, `AddNotificationsServices` when `DisableDataLake` is true.
- `Journeys.DTO/Models/NotificationOutcomePayload.cs` — closed webhook body (no raw event JSON, ledger rows, or secrets).
- `Journeys.Core/RulesEngine/Outcomes/NotificationOutcome.cs` — Calculate loads tenant config; Award POSTs via `state.NotificationService`.
- `Journeys.Core/RulesEngine/Engine/RulesEngineState.cs` — `NotificationService` and `TreatNotificationSendThrowAsFalse`; copied in `NavigatePayload` and from `RulesServiceRequest`.
- `Journeys.Core/RulesEngine/Engine/RulesServiceRequest.cs` — port and host switch on the request (not the `RulesService` constructor).
- `Journeys.Core/Services/RulesService.cs` — award loop no longer forces `IsAwarded = true`.
- `Journeys.Core/Services/EventService.cs` — copies `INotificationService` and `Journeys:NotificationOutcome:TreatSendThrowAsFalse` onto the request.
- `Journeys.Core/Configuration/ConfigureNotifications.cs` — always registers `INotificationService`.
- `Journeys.API/Mcp/RulesEngineMcpContractSummary.cs` — `MatrixVersion` `2026-09-18` + `notification_config_id` critical row.
- `Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs` — version and row assertions; deposit/spend rows kept.
- Meaning docs: `docs/product/ontology/outcome.md`, `loyalty-account.md`, `rule.md`, `docs/product/use-cases/process-event.md`; `docs/product/graph/path-map.yaml` (`Journeys.Notification` `meaningOptional: false`); `docs/platform/architecture.md`, `runtime.md`; `docs/developer/testing.md`, `index.md`.

## Implementation decisions

- Send port lives on `RulesEngineState` / `RulesServiceRequest`. Production `EventService` attaches it. `RulesService` constructor is unchanged. Award signature is unchanged.
- Calculate returns null when `NotificationConfigId` is missing, the port is missing, or the tenant-scoped config is missing, inactive, or EventModelId-mismatched.
- Award send throw fails the event unless `TreatNotificationSendThrowAsFalse` is set on state (copied from `Journeys:NotificationOutcome:TreatSendThrowAsFalse`).
- `CalculateOnly` skips send. Payload field list is closed on the DTO.

## Tests

- `FullyQualifiedName~NotificationOutcomeTests` + `FullyQualifiedName~RulesEngineMcpContractSummaryTests`: **25 passed** (9 NotificationOutcome including DI companion + MCP class).
- Verify: `.\scripts\aidlc-agent-verify-sensor.ps1 -Files` (product + meaning docs) **exit 0**. Unit-scoped filters are this unit's test oracle; the full `Journeys.Tests` suite was not used.

## Deviations

- Developer session could not run tests. Conductor ran them after: compile failed on duplicate `RulesServiceRequest` properties (removed); tests failed until `CreateState` initialized `StubPointAccountTypeCache` (same pattern as hydrate/cascade tests).
- `EventService` gained optional constructor parameters `INotificationService` and `IConfiguration` so production can copy the port and host switch onto `RulesServiceRequest`. Not a `RulesService` constructor change.
- `docs/product/ontology/campaign.md`, `draft-live.md`, and `event-model.md` gained one-line webhook notes so docs-impact could include the `RulesService` / `EventService` / `ConfigureNotifications` paths.
