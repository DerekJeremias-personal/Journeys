# Entities — `u1-tree-hydrate`

Logical shapes this unit reads. No new persist store. Tenant scope is implied on every account and campaign.

```yaml
entities:
  - name: JourneyNode
    description: A node on a Live campaign journey. Hydrate walks this tree, not only the root RuleSets.
    attributes:
      - { name: rules, type: RuleSet[], required: false, unique: false, references: RuleSet, constraints: "earn RuleSets on this node" }
      - { name: navConstraints, type: NavConstraint[], required: false, unique: false, references: NavConstraint }
      - { name: children, type: JourneyNode[], required: false, unique: false, references: JourneyNode }
    constraints: []
    relationships:
      - { to: JourneyNode, cardinality: "1:N", direction: parent-to-children }
      - { to: RuleSet, cardinality: "1:N", direction: node-to-earn-rules }
      - { to: NavConstraint, cardinality: "1:N", direction: node-to-nav }

  - name: NavConstraint
    description: Navigation constraint that may itself nest composites and carry historical or taxonomic rules.
    attributes:
      - { name: rules, type: Rule[], required: false, unique: false, references: Rule }
      - { name: children, type: NavConstraint[], required: false, unique: false, references: NavConstraint }
    constraints: []
    relationships:
      - { to: NavConstraint, cardinality: "1:N", direction: composite-to-nested }

  - name: RuleSet
    description: Named rule collection on a node. Historical and taxonomic members must be collected for decay.
    attributes:
      - { name: rules, type: Rule[], required: false, unique: false, references: Rule }
    constraints: []
    relationships: []

  - name: Rule
    description: Historical or taxonomic rule that participates in TTL decay and RuleState.
    attributes:
      - { name: kind, type: string, required: true, unique: false, allowed_values: [HistoricalRule, TaxonomicRule] }
      - { name: ttl, type: duration, required: false, unique: false }
    constraints: []
    relationships: []

  - name: LoyaltyAccount
    description: Existing account whose RuleState is loaded and upserted after hydrate. Not reshaped here.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: ruleState, type: object, required: false, unique: false }
    constraints:
      - tenant isolation — only this tenant's RuleState
    relationships: []
```

## Summary

Hydrate collect walks `JourneyNode` earn `RuleSet`s, `NavConstraint` trees (including nested composites), and `Children`. `LoyaltyAccount.RuleState` stays the existing persist shape.
