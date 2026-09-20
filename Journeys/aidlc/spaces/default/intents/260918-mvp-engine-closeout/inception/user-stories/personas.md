# Personas — MVP engine closeout

No consumer / end-customer persona. `Journeys.UX` is out of increment. Value is observable on ProcessEvent and in meaning docs.

## P1 program-operator (primary)

| Field | Detail |
|-------|--------|
| **Name** | program-operator |
| **Role** | Configures PATs, journeys, RuleSets, and tenant webhook configs so a loyalty program runs on inbound events |
| **Goals** | Escrow hold then Spendable then Expired at earn+365; later events see correct historical/taxonomy state; a Live RuleSet can POST the tenant webhook |
| **Pain points** | Holds do not release before this event’s rules; cascade clocks restart at the move; child-node history does not decay; NotificationOutcome never fires |
| **Tech comfort** | High (campaign/PAT authoring via existing API/MCP, not new UX) |
| **Frequency** | Configures occasionally; cares every time a member event is processed |
| **Priority** | Primary — owns the “so that” on Must stories US1.1–US4.1 |

## P2 technical-buyer (secondary)

| Field | Detail |
|-------|--------|
| **Name** | technical-buyer |
| **Role** | Evaluates the engine contract: onion seams, MCP matrix, product meaning vs code |
| **Goals** | NotificationOutcome is a real Kind with a required NotificationConfigId; docs/graph match shipped behavior; no new capability ids |
| **Pain points** | Contract summary still lists NotificationOutcome without a critical row; ontology still describes unimplemented webhooks |
| **Tech comfort** | High |
| **Frequency** | Review and integration checkpoints |
| **Priority** | Secondary — owns US5.1 (docs/graph). MCP proof is AC on US4.1, not a separate story |

## Relationships

- program-operator depends on technical-buyer only for contract/docs honesty, not for a second UI.
- technical-buyer does not replace the operator on Must stories.
