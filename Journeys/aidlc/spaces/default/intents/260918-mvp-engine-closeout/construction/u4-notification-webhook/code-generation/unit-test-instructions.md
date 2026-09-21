# Unit test instructions — `u4-notification-webhook`

## Runner

Existing xUnit 2.9.3 on `Journeys.Tests`. No new test project. No BDD files.

## Commands (this unit only)

New Calculate/Award seam:

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~NotificationOutcomeTests
```

MCP row (historical file; update assertions only; not the webhook oracle):

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~RulesEngineMcpContractSummaryTests
```

Do not use a bare `dotnet test` for this unit's Red/Green loop.

## New file

`Journeys.Tests/RulesEngine/Outcomes/NotificationOutcomeTests.cs` — 8 tests, Arrange–Act–Assert.

| # | Case | AC / BR |
|---|------|---------|
| 1 | Happy Award POSTs closed payload: tenantId, loyaltyAccountId, extAccountId if present, campaignId, ruleSetId, issuingOutcomeId, issuingOutcomeKind, eventId, eventType, eventModelId, awardedAtUtc. Fake send captured those fields only. | AC4.1.1, BR4.3 |
| 2 | Missing NotificationConfigId → Calculate null | AC4.1.2, BR4.1 |
| 3 | Missing, inactive, or EventModelId-mismatched config → Calculate null. Config load uses `state.TenantId` so a foreign-tenant config id is a miss. | AC4.1.2, BR4.2 |
| 4 | Send false → Award `IsAwarded` false; sibling awards not rolled back | AC4.1.3, BR4.4 |
| 5 | Send throw + default fail-closed → exception propagates (fails ProcessEvent) | AC4.1.4, BR4.5 |
| 6 | Send throw + `TreatNotificationSendThrowAsFalse` on state → same as send false | AC4.1.7, BR4.5 |
| 7 | CalculateOnly true → Calculate may resolve; Award/send do not run | AC4.1.5, BR4.6 |
| 8 | Award returns `IsAwarded` false and the award loop does not force true. Award signature stays `(RulesEngineState, ILoyaltyAccountService, CancellationToken)`. | AC4.1.9, BR4.9 |

Fake `INotificationService` on `RulesEngineState`. Do not reference `Journeys.Notification`. Do not extend the `RulesService` constructor.

Host DI (AC4.1.8): cover by asserting `AddNotificationsServices` registers `INotificationService` when `DisableDataLake` is true — either as case 8's companion in this file if DI is cheap to spin, or as a ninth fact in the same class. Do not start the API host.

## Historical file (test-after)

`Journeys.Tests/Mcp/RulesEngineMcpContractSummaryTests.cs`

- Assert `MatrixVersion` is `2026-09-18`.
- Assert a critical row requiring `NotificationConfigId` on `NotificationOutcome`.
- Leave deposit/spend rows.
- Do not retarget `JourneyContractSummaryPinArtifactsTests` or other fixtures that embed `2026-06-20` as sample JSON.

## Coverage

No in-repo coverage floor. Keep the existing suite green. Coverlet stays a collector.

## Mocks

Fake `INotificationService` on `RulesEngineState` (same injection style as `LoyaltyAccountService`). In-memory config lookup. No Cosmos. No HTTP. Tenant-scoped `GetNotificationConfigAsync`.

## Data

Arrange an active `rest_api` config for this event's tenant. Do not log webhook auth headers or raw event JSON.
