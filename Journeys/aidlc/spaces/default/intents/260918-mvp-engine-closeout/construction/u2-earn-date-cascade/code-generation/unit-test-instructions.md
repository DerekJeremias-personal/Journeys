# Unit test instructions — `u2-earn-date-cascade`

## Runner

Existing xUnit 2.9.3 on `Journeys.Tests`. No new test project. No BDD files.

## Commands (this unit only)

New cascade-clock seam:

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~LoyaltyAccountExpirationCascade
```

Historical outcome suite (must stay green; not the cascade oracle):

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~ExpirePointsOutcomeTests
```

Do not use a bare `dotnet test` for this unit’s Red/Green loop.

## New file

`Journeys.Tests/Services/LoyaltyAccountExpirationCascadeTests.cs` — 5–8 tests, Arrange–Act–Assert.

| # | Case | AC / BR |
|---|------|---------|
| 1 | Escrow (30d) hops to dest with 365 days; EarnDate unchanged; dest ExpirationDate = EarnDate + 365 (not UtcNow + 365) | AC2.1.1, BR2.1, BR2.3 |
| 2 | Dest PointsLifespanEndDate set → dest ExpirationDate is that instant | AC2.1.4, BR2.2 |
| 3 | Dest has neither end-date nor days → dest ExpirationDate unset (no 100-day default) | AC2.1.5, BR2.4 |
| 4 | Dest PAT missing / not found for tenant → row stays in source | AC2.1.3, BR2.6 |
| 5 | Dest-PAT load throws unexpectedly → exception propagates (fails ProcessEvent) | AC2.1.6, BR2.7 |
| 6 | Dest days set, EarnDate missing → existing ExpirationDate last resort; never UtcNow as lifespan base | BR2.5 |
| 7 | Already-cascaded UtcNow dest date is left as written when this hop does not run again | AC2.1.2, BR2.8 |
| 8 | Optional: dest end-date wins even when dest also has days | BR2.2 |

Arrange dest PAT with **365 days**, not the 30-day Spendable factory.

## Coverage

No in-repo coverage floor. Keep the existing suite green. Coverlet stays a collector.

## Mocks

Reuse existing `Journeys.Tests` stubs (`StubPointAccountTypeCache`, `TestDataFactory`). Fake ledger adapter ports. Do not reference `Journeys.Notification`. Do not extend the `RulesService` constructor.

## Data

In-memory ledgers + PAT cache. No Cosmos required. Tenant-scoped dest PAT load.
