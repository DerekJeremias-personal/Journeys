# Unit test instructions — `u1-tree-hydrate`

## Runner

Existing xUnit 2.9.3 on `Journeys.Tests`. No new test project. No BDD files.

## Commands (this unit only)

New collect seam:

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~HydrateCollect
```

Historical root suite (must stay green; not the collect oracle):

```powershell
dotnet test Journeys.Tests/Journeys.Tests.csproj --filter FullyQualifiedName~RuleServiceTests
```

Do not use a bare `dotnet test` for this unit’s Red/Green loop.

## New file

`Journeys.Tests/Services/HydrateCollectTests.cs` — 5–8 tests, Arrange–Act–Assert.

| # | Case | AC / BR |
|---|------|---------|
| 1 | Child-node HistoricalRule is collected; second ProcessEvent applies TTL decay and RuleState load | AC1.1.1, BR1.1 |
| 2 | Child-node TaxonomicRule is collected (same path as historical) | FR1, advisory R-01 |
| 3 | Nested NavConstraint HistoricalRule is collected | AC1.1.2, BR1.2 |
| 4 | Empty Rules on a child or nav node does not throw | AC1.1.2, BR1.3 |
| 5 | Empty collect still runs TTL / decay / RuleState / upsert (does not skip the pipeline) | BR1.4, advisory R-02 |
| 6 | Root-only historical hydrate still works | AC1.1.3, BR1.4 |
| 7 | Nested composite NavConstraint is walked | BR1.2 |
| 8 | Optional: grandchild earn RuleSet collected | BR1.1 |

## Coverage

No in-repo coverage floor. Keep the existing suite green. Coverlet stays a collector.

## Mocks

Reuse existing `Journeys.Tests` stubs (`StubHistoricalRuleStateTTLAdapter`, `TestJourneyFactory`, `TestDataFactory`). Fake ports on `RulesEngineState`. Do not reference `Journeys.Notification`. Do not extend the `RulesService` constructor.

## Data

Build journeys in-memory: root + child RuleSets, nested `SimpleNavigationCriteria.NavConstraint`. No Cosmos or blob required for these cases.
