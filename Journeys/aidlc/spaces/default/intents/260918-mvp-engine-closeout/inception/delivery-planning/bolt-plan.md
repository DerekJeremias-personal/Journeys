# Bolt plan — MVP engine closeout

A **Bolt** is one Construction pass over a slice of work that ends in something that runs (units, a done check, and a confidence hypothesis). Sequence is economic (Delivery Planning Q1–Q4). Topology stays the Units Generation DAG: expire-on-process (`u3-expire-on-process`) depends on earn-date cascade (`u2-earn-date-cascade`).

**Walking skeleton:** none. Team practices already skip a second thin end-to-end ceremony; `Journeys.API` → Core → DAL / Infra / Notification already exists.

**Sources:** `delivery-planning-questions.md` (Looks correct 2026-09-19), `unit-of-work.md`, `unit-of-work-dependency.md`, `unit-of-work-story-map.md`, `contract-summary.md`, `team-practices.md`, `linear-map.yaml`.

## Sequence

| Order | Bolt | Units | Linear | Stories |
|-------|------|-------|--------|---------|
| 1 | B1 Tree hydrate | `u1-tree-hydrate` | [JOU-1](https://linear.app/bishoplabs/issue/JOU-1) | US1.1 |
| 2 | B2 Earn-date cascade | `u2-earn-date-cascade` | [JOU-2](https://linear.app/bishoplabs/issue/JOU-2) | US2.1 |
| 3 | B3 Expire + webhook | `u3-expire-on-process`, `u4-notification-webhook` | [JOU-3](https://linear.app/bishoplabs/issue/JOU-3), [JOU-4](https://linear.app/bishoplabs/issue/JOU-4) | US3.1, US4.1, US5.1 |

Serial in this session. No Bolt is the walking skeleton. Docs/graph increment-close rides U4 inside B3.

## Bolt B1 — Tree hydrate

- **Units:** `u1-tree-hydrate`
- **Walking skeleton:** no
- **Stories / AC:** US1.1 / AC1.1.1–AC1.1.3
- **Contracts:** C1 appendix (hydrate collect). No `EventService` or ledger change.
- **Definition of done:** TDD on the collect seam first. Child-node and NavConstraint (including nested) historical/taxonomic rules are collected and decayed; empty `Rules` does not throw; existing root TTL / decay / `RuleState` / upsert tests stay green. `scripts/aidlc-agent-verify-sensor.ps1` (`-RunTests` because tests change). Claim JOU-1 before any `Journeys.*` write. No new host, capability id, or UX.
- **Confidence hypothesis:** A second ProcessEvent on the same account applies TTL decay and loads `LoyaltyAccount.RuleState` for a child-node RuleSet the same way it already does for root RuleSets.
- **Expected demo:** ProcessEvent twice against a Live campaign whose child / nav rules have HistoricalRule; show decay + RuleState on the second event.

## Bolt B2 — Earn-date cascade

- **Units:** `u2-earn-date-cascade`
- **Walking skeleton:** no
- **Stories / AC:** US2.1 / AC2.1.1–AC2.1.6
- **Contracts:** C2 (earn-date hop). Human-gated ledger/money-like math.
- **Definition of done:** TDD on `SaveLedgerExpirations`. Hop keeps `EarnDate`; dest expiration is dest end-date, else earn + dest days, else unset; last-resort existing `ExpirationDate` only when dest days exist and earn is missing; missing dest PAT stays in source; dest-PAT infra throw fails ProcessEvent; already-cascaded UtcNow rows stay. Arrange dest PAT with 365 days (not the 30-day Spendable factory). Claim JOU-2 before `Journeys.*` writes. Human reviews ledger clock before merge.
- **Confidence hypothesis:** Escrow 30 → Spendable 365 keeps EarnDate and writes dest expiration as earn + 365 days, not move + 365.
- **Expected demo:** Fixture EarnDate 2026-01-01 hops to a 365-day dest PAT; dest `ExpirationDate` is 2027-01-01.

## Bolt B3 — Expire-on-process + webhook

- **Units:** `u3-expire-on-process`, `u4-notification-webhook`
- **Walking skeleton:** no
- **Stories / AC:** US3.1 / AC3.1.1–AC3.1.3; US4.1 / AC4.1.1–AC4.1.9; US5.1 / AC5.1.1–AC5.1.3
- **Contracts:** C1 ProcessEvent order; C2 consumed (no second dest clock); C3 webhook payload; C4 MCP `NotificationConfigId` row (`MatrixVersion` `2026-09-18`). Shared `EventService` edit site: lock → bring-current → rules, plus `RulesEngineState.NotificationService` host-copy.
- **Definition of done:** TDD on lock → bring-current → navigate/rules, then NotificationOutcome Calculate/Award. This event can spend a released hold; existing `TryLockAccount` only; invalid/missing accounts skip bring-current. Live RuleSet POSTs closed `NotificationOutcomePayload` via existing `rest_api`; Award honors `IsAwarded` false; throw fails ProcessEvent unless host switch; MCP row lands; US5.1 docs/graph in the same change. Claim JOU-3 and JOU-4 before `Journeys.*` writes. Increment-close `docs-impact` / `graph-impact` / `aidlc-agent-verify-sensor.ps1` (`-RunTests`).
- **Confidence hypothesis:** After the existing lock, due Escrow is current before rules so this event can spend it; a matching Live NotificationOutcome POSTs the tenant webhook without a second bus; meaning docs match the shipped seams.
- **Expected demo:** One ProcessEvent: due hold hops under lock, then a Live NotificationOutcome hits the fake `INotificationService`; show docs-impact / graph-impact green.

## Construction notes

- **Iteration:** keep the default stage-major walk (Functional Design for each unit, then later stages). Bolts above are delivery slices and demo order, not a unit-major stage cascade.
- **Claim rule:** Linear issue claimed before that unit’s `Journeys.*` product code. Pull-back copies already exist under `units-generation/unit-linear-copy/`.
- **Verify:** `scripts/aidlc-agent-verify-sensor.ps1`; add `-RunTests` when tests change. Humans merge and release.
