# Integration test instructions

Standard strategy: unit tests plus integration tests for key boundaries. No second test project. No BDD. `Journeys.UX` Vitest is out of increment.

## Framework

xUnit 2.9.3 in `Journeys.Tests`. Tests stay in-process with fakes. `Journeys.Tests` must not reference `Journeys.Notification`; fake `INotificationService` on `RulesEngineState`.

## Key boundaries this increment

| Boundary | What it proves | Filter |
|----------|----------------|--------|
| Hydrate → collect | Tree-wide `FlattenToRulesOfType` after hydrate | `FullyQualifiedName~HydrateCollect` |
| Cascade dest clock | EarnDate preserved; dest expiration from dest PAT | `FullyQualifiedName~LoyaltyAccountExpirationCascade` |
| Lock → bring-current → rules | Expire-on-process under the existing `TryLockAccount` lease | `FullyQualifiedName~ProcessCampaignsExpireOnProcess` |
| Rules → NotificationOutcome | Calculate/Award + optional send-throw switch | `FullyQualifiedName~NotificationOutcomeTests` |
| MCP contract | `MatrixVersion` `2026-09-18` + `NotificationConfigId` critical row | `FullyQualifiedName~RulesEngineMcpContractSummaryTests` |

The cross-unit seam is ProcessEvent order: lock → `BringLoyaltyAccountPointsCurrentInternalAsync` (assign `PointLedgers`) → `AttachNotificationPort` → `ProcessRulesAsync`. U3 owns the order; U4 owns the port. Run both oracles; do not invent an unlocked second expire path.

## How to run

From `C:\Dev\Journeys\Journeys`, once each (do not run a bare `dotnet test` for this stage):

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~HydrateCollect
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~LoyaltyAccountExpirationCascade
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~ProcessCampaignsExpireOnProcess
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~NotificationOutcomeTests
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~RulesEngineMcpContractSummaryTests
```

## Coverage targets

No in-repo coverage floor. Coverlet stays a collector. Volume target is five to eight new facts per closeout component (U4: eight facts; optional ninth DI companion).

## Test data

Arrange–Act–Assert with in-memory fakes. TenantId on every operation. Do not use production payloads or live webhook endpoints.
