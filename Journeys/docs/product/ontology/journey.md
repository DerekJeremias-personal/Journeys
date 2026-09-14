# Ontology: journey

A **journey** is a stateful **tree of program steps** (nodes, navigation gates, earn RuleSets). It is how multi-step behavior is encoded — not flat if/else in a controller, and not the campaign shell. Capability id: `journeys`.

The campaign **owns** the root node (`Campaign.Journey`). This file owns the graph. Authoring UX is out of this solution; APIs and the campaign agent author the same tree the engine runs.

Journeys are **criteria-based progression by account**. Membership can change tier-like position, and a point deposit may read the outcome definition, **all current journey nodes**, and other rule factors (`docs/product/ontology/loyalty-account.md`, `outcome.md`).

## Shape (`JourneyNode`)

| Piece | Meaning |
|-------|---------|
| **Node** | `Id`, `Name`, optional `Children`. `RootNodeId` is the campaign’s root id (filled on save if missing). |
| **Children** | Nested steps (often tiers). Entry into a child is **additive** (does not by itself remove the parent). |
| **Navigation** | Per-channel criteria: `Entry`, `Exit`, `Transition` (`NavigationType`). Each value is typically `SimpleNavigationCriteria` (`$type`) with a `NavConstraint` (`RuleBase`) and optional navigation outcomes. |
| **Rules** | Earn **RuleSets** on the node (`docs/product/ontology/rule.md`). Evaluated only if the account is **still in** this node after navigation. |

`JourneyBase` is only the id-bearing base. Do not invent another journey aggregate.

## Process order (`JourneyNode.ProcessAsync`)

1. Evaluate navigation (including sibling transitions if already in this node).
2. Apply membership changes and navigation outcomes.
3. If **Exit** or **Transition** away — do **not** run this node’s RuleSets; recurse into the transition target when leaving via Transition.
4. If still in the node, evaluate each RuleSet; on `true`, calculate outcomes (`CampaignId` + `RuleSetId`).
5. Evaluate **Entry** into children; if the gate passes, enter (additive) and `ProcessAsync` the child.

Account membership is stored as `RootJourneyNodeId` + `JourneyNodeIds` on the loyalty account (`LoyaltyAccountJourney`), not on the campaign document.

## Governance for agents

- Put earn outcomes on node **RuleSets**. Use `NavConstraint` to gate membership (tier bands, entry). Do not conflate the two (`rule.md`).
- Root Entry is often an unconditional `SimpleRule`; tier bands are Transition `AndRule` + `PointBalanceProvider` (pattern `tier-navigation-point-balance`).
- Keep `RootNodeId` consistent down the tree. New child nodes need ids before upsert.
- Execution walks **Live** campaign journeys. Draft trees are for authoring and `process_event(campaignId)` verification (`draft-live.md`).
- If this file and `JourneyNode` disagree, **code is canonical**.

## Related

`campaign.md`, `rule.md`, `outcome.md`, `loyalty-account.md`.
