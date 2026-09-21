# Ontology: outcome

An **outcome** is what the engine delivers on a **positive** evaluation. Capability id: `outcomes`. Do not invent a second capability for points, notifications, tags, or progression.

Outcomes use **`Kind`**, not `$type` (`OutcomeKindDiscriminators`). Nested providers on an outcome still use `$type`. Closed list: `get_rules_engine_contract_summary`.

## Two placements (do not conflate)

1. **RuleSet outcomes** — on a journey node’s RuleSet. Run after navigation if the account is still in that node (`rule.md`, `journey.md`).
2. **Navigation outcomes** — on Entry/Exit/Transition criteria. Membership change, not earn RuleSets.

A point deposit may read the outcome definition, **all current journey nodes**, and other rule factors (`journey.md`, `loyalty-account.md`).

## Calculate then award

`CalculateOutcomeAsync` sizes the result (`IsAwarded = false`). `AwardOutcomeAsync` writes ledgers/tags. `CalculateOnly` stops after calculate (`RulesService`). Reprocessing the same event is wrapper idempotence (`event-model.md`), not a second deposit invented in the agent.

## Product-facing kinds

| Kind | Code | Meaning |
|------|------|---------|
| Points | `DepositPointsOutcome`, `SpendPointsOutcome`, `ExpirePointsOutcome` | Deposit / spend / expire against **PAT GUIDs** in `AffectedPointAccountTypeIds` (PascalCase). Deposit uses `PointsPerDollar` × `DollarAmountProvider`. Expire hops via `ExpiresToPointAccountTypeId` using the dest PAT clock (`loyalty-account.md`): keep `EarnDate`; dest expiration is dest end-date, else earn + dest days, else unset. |
| Notification | `NotificationOutcome` | Live RuleSet Award POSTs a closed `NotificationOutcomePayload` to the tenant `rest_api` webhook (`NotificationConfigId`). Missing/inactive/mismatched config → Calculate null. Send false keeps `IsAwarded` false; send throw fails ProcessEvent unless `Journeys:NotificationOutcome:TreatSendThrowAsFalse`. `CalculateOnly` does not POST. No email/Twilio this increment. |
| Journey progression | navigation, not an outcome `Kind` | Per-account membership (`journey.md`). |
| Tags | `TagOutcome` | `TagLoyaltyAccountAsync` on positive evaluation. |

## Also implemented (do not promote)

`WorkflowOutcome`, `ThirdPartyOutcome`, `RuleStateOutcome` exist under `RulesEngine/Outcomes`. Document only so agents do not invent parallel types.

## Governance for agents

- PAT ids are manifest GUIDs, never `spendable` / `SPENDABLE_PAT_ID` (`loyalty-account.md`, `CampaignBuildGate`).
- Ledger / money-like outcomes are human-gated (`AGENTS.md`).
- Persist `outcomestates` / `issuingoutcomeid` lowercase on the wrapper (`event-model.md`).
- If this file and `OutcomeBase` disagree, **code is canonical**.

## Related

`rule.md`, `journey.md`, `loyalty-account.md`, `event-model.md`.
