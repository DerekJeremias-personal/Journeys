# Rules — `u1-tree-hydrate`

```yaml
rules:
  - id: BR1.1
    statement: Child-node earn RuleSets are collected and decayed the same way root RuleSets already are.
    category: policy
    applies_to: JourneyNode.rules
    trigger: HydrateState on ProcessEvent
    logic: IF a Live campaign has a child-node RuleSet with a HistoricalRule THEN that rule is in the collected set and TTL decay plus RuleState load apply on a later event for the same account.
    violation: Later events see stale child progress.
    source: FR1, FR1.1, AC1.1.1

  - id: BR1.2
    statement: NavConstraint trees including nested composites are collected.
    category: policy
    applies_to: NavConstraint
    trigger: HydrateState on ProcessEvent
    logic: IF a HistoricalRule sits on a NavConstraint (including a nested/composite nav tree) THEN that rule is collected and decayed.
    violation: Nav historical rules never decay.
    source: FR1.2, AC1.1.2

  - id: BR1.3
    statement: An empty Rules collection does not fail hydrate.
    category: constraint
    applies_to: RuleSet.rules, NavConstraint.rules
    trigger: Collect walk
    logic: IF Rules is empty or missing THEN continue; do not throw.
    violation: ProcessEvent fails on empty rule lists.
    source: FR1.2, AC1.1.2

  - id: BR1.4
    statement: Existing TTL fetch, decay, LoyaltyAccount.RuleState load, and finally upsert stay.
    category: constraint
    applies_to: HydrateState
    trigger: After collect
    logic: IF rules were collected THEN apply the existing hydrate pipeline; do not invent a second decay path.
    violation: Root-only tests break or decay diverges.
    source: FR1.3, FR1.4, AC1.1.3

  - id: BR1.5
    statement: This unit does not change EventService or ledgers.
    category: constraint
    applies_to: unit boundary
    trigger: Design and implementation
    logic: IF the change is hydrate collect THEN it lives on RulesService.HydrateState and JourneyNode helpers only.
    violation: Shared ProcessEvent order or PAT math is edited here.
    source: US1.1, C1 appendix
```

## Summary

| ID | Category | Trigger |
|----|----------|---------|
| BR1.1 | policy | Child earn RuleSets |
| BR1.2 | policy | Nav trees |
| BR1.3 | constraint | Empty Rules |
| BR1.4 | constraint | Keep existing hydrate pipeline |
| BR1.5 | constraint | No EventService / ledger |
