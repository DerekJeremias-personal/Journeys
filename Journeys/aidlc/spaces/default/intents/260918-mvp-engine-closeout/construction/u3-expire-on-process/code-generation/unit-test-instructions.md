# Unit test instructions — `u3-expire-on-process`

## Runner

Existing xUnit 2.9.3 on `Journeys.Tests`. No new test project. No BDD files.

## Commands (this unit only)

New expire-before-rules seam:

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~ProcessCampaignsExpireOnProcess
```

Historical GET/reconcile expire (must stay green; not this unit’s oracle):

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~UserPointsTests
```

Do not use a bare `dotnet test` for this unit’s Red/Green loop.

## New file

`Journeys.Tests/Services/ProcessCampaignsExpireOnProcessTests.cs` — 5–8 tests, Arrange–Act–Assert.

| # | Case | AC / BR |
|---|------|---------|
| 1 | After `TryLockAccount`, `BringLoyaltyAccountPointsCurrentInternalAsync` runs before `ProcessRulesAsync`. `PointLedgers` on the account passed to rules match the bring-current return (released hold). | AC3.1.1, BR3.1, BR3.2 |
| 2 | Call order is lock → bring-current → `AttachNotificationPort` → `ProcessRulesAsync`. Request still carries `INotificationService` when the host supplied one. | AC3.1.1, BR3.1 |
| 3 | Bring-current throw after lock → exception propagates; `ProcessRulesAsync` is never called. | AC3.1.3, BR3.7 |
| 4 | `TryLockAccount` still uses today’s lease + retries. No new lock type. Bring-current is not called if the lock is not acquired. | AC3.1.2, BR3.6 |
| 5 | `ProcessEventInternalAsync` still has no bring-current call (skip invalid / missing / pre-save remains those existing branches). The only new call site is `ProcessCampaignsAsync`. | AC3.1.3, BR3.4, BR3.5 |
| 6 | GET/reconcile call sites (~428 / ~589) and `ResettleAccountInternalAsync` are not rewritten this unit. | AC3.1.2, BR3.8 |
| 7 | EventService does not compute dest expiration (no second clock). Hops inside bring-current stay on C2. | BR3.3 |
| 8 | Optional: `ExpirePoints` lease-preserve from U2 is consumed, not reimplemented. | BR3.3 |

Fake `ILoyaltyAccountService` (`TryLockAccount`, `BringLoyaltyAccountPointsCurrentInternalAsync`) and `IRulesService` (`ProcessRulesAsync`) on `EventService`. Stub remaining ctor deps. Do not reference `Journeys.Notification`. Do not extend the `RulesService` constructor. Do not close AC3.1.1 on `UserPointsTests`.

Mark `ProcessCampaignsAsync` `internal` if tests cannot reach it otherwise (`InternalsVisibleTo("Journeys.Tests")` already exists). Do not extract a new service.

## Historical file (test-after)

`Journeys.Tests/Services/UserPointsTests.cs`

- Must stay green.
- Not the ProcessEvent lock → expire → rules oracle.

## Coverage

No in-repo coverage floor. Keep the existing suite green. Coverlet stays a collector.

## Mocks

In-memory fakes. Record call order on lock, bring-current, and `ProcessRulesAsync`. No Cosmos. No HTTP. Tenant-scoped account id.

## Data

Arrange a valid locked account with a due Escrow row (`ExpirationDate <= UtcNow`). Bring-current fake returns released `PointLedgers`. Do not log secrets.
