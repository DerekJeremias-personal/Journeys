# User Stories Assessment

## Decision

**Decision:** Execute

## Rationale

**Rationale:** This increment is Core engine closeout, not operator UX. Stories still add value: two named personas (`program-operator`, `technical-buyer`), complex ledger/cascade/webhook logic, and cross-team seams (Core, Notification, Tests, docs). Requirements alone are implementable; stories give Given/When/Then AC and Linear-sized slices for units-generation.

**Factors considered**

| Factor | Signal |
|--------|--------|
| Project type | Brownfield Classic; ingested approved spec + requirements |
| User-facing scope | No `Journeys.UX` this increment. Value is observable on ProcessEvent (holds release, expire-at-earn+365, webhook POST). |
| Complexity | Earn-date cascade, lock-then-bring-current, webhook throw policy |
| Cross-team | Core + Notification adapters + MCP contract + docs/graph |
| Skip criteria | Not a pure refactor, isolated bug, infra-only, or developer-tooling change |

**Where stories add the most value**

- ProcessEvent truth for the 30 / 365 / Expired program (FR2, FR3)
- Outcome-fired tenant webhook (FR4)
- Child-node / nav historical hydrate so later events behave (FR1)
- Error edges already decided (bring-current throw, missing dest PAT, webhook throw switch)

**Alternative if we had skipped:** `requirements.md` FR/NFR IDs plus the plan’s four units. That remains the construction source; stories sit on top, they do not replace it.
