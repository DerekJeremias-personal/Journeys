# Code summary — `u1-tree-hydrate`

## Files created or modified

- `Journeys.Tests/Services/HydrateCollectTests.cs` — eight collect-seam tests (child historical TTL, child taxonomic, nav historical, empty Rules, empty collect pipeline, root-only, nested composite nav, grandchild earn).
- `Journeys.Tests/Stubs/StubTaxonomyDataAdapter.cs` — `GetManyTaxonomiesByXidAsync` is virtual so the taxonomic collect test can record hydrate loads.
- `Journeys.Core/RulesEngine/Journey/JourneyNode.cs` — `FlattenToRulesOfType` now collects this node’s earn RuleSets, each NavConstraint tree (including nested composites), and children. Empty or null Rules skip without throw.
- `Journeys.Core/Services/RulesService.cs` — `HydrateState` collects historical and taxonomic rules via `JourneyNode.FlattenToRulesOfType` instead of only root `Journey.Rules`. Existing TTL fetch, decay, RuleState load, and upsert are unchanged.
- Meaning docs: `docs/product/ontology/rule.md`, `journey.md`, `campaign.md`, `draft-live.md`; `docs/platform/architecture.md`; `docs/developer/testing.md`.

## Implementation decisions

- One collect helper on `JourneyNode` is the oracle. Hydrate does not invent a second decay path.
- Root campaigns with empty `Rules` are no longer skipped; the tree is walked anyway.
- No `EventService`, ledger, API, repository, migration, frontend, Docker, or `RulesService` constructor change. No `Journeys.Notification` reference.

## Tests

- New: `FullyQualifiedName~HydrateCollect` (8 cases) — Passed.
- Historical root suite remains `FullyQualifiedName~RuleServiceTests` (14 cases, not retargeted as the collect oracle) — Passed.
- Combined filter `FullyQualifiedName~HydrateCollect|FullyQualifiedName~RuleServiceTests`: Passed 22/22 (812 ms).
- `.\scripts\aidlc-agent-verify-sensor.ps1` with source-manifest product/docs files: docs-impact OK, graph-impact OK (`campaigns`, `rules-engine`, `journeys`), build succeeded, `agent-verify: OK`.
- Bare `-RunTests` on the whole `Journeys.Tests` project is not this unit’s oracle. It failed on pre-existing `BatchJobAdapterIntegrationTests` (`BackendSystemException` talking to the backend). Unit instructions keep the Red/Green loop on the two filters above.

## Deviations

- Compile-fail Red (`RulesServiceRequest` missing using) was recorded by the human. The using was added (`Journeys.Core.RulesEngine.Engine` plus `Journeys.Core`). Behavioral Red after that compile fix could not be captured in this session because the test command was blocked; Green was implemented against the approved collect contract.
