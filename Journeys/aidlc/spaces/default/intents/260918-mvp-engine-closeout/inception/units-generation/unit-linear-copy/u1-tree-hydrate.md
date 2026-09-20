# U1 — `u1-tree-hydrate`

## U1 — `u1-tree-hydrate`

**Description:** Tree-wide hydrate so child-node and nav historical/taxonomic rules decay like root RuleSets.

**Boundaries:** `RulesService.HydrateState` and `JourneyNode` collect helpers. Does not change ledgers or NotificationOutcome.

**Responsibilities:** Collect earn + nav rules recursively; keep existing TTL/decay/RuleState/upsert; TDD on the collect seam.

**Constraints:** Do not lock root-only `RuleServiceTests` as the new oracle. No `EventService` change.
