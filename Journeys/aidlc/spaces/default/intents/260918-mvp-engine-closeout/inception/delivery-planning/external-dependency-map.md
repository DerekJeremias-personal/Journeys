# External dependency map — MVP engine closeout

A **Bolt** is one Construction pass over a slice of work that ends in something that runs. This map lists anything outside that pass that can hold it. Delivery Planning Q5: nothing in-tree waits on another team.

**Sources:** `team-practices.md`, `contract-summary.md`, `linear-map.yaml`, `bolt-plan.md`.

## In-tree

| Item | Owner | Blocks | Status |
|------|-------|--------|--------|
| Existing `POST /api/Events/{tenantId}/{modelName}/process` | This increment (U3 owns order only) | None — no new route | Present |
| `NotificationConfig` + `rest_api` adapter | Tenant config already in product; `Journeys.Notification` | B3 webhook | Present; no new adapter |
| Dest PAT / ledger persist | Existing DAL / Backend | B2, then B3 bring-current | Present |
| Linear JOU-1–JOU-4 | Projection already upserted; pull-back on disk | Claim before that unit’s `Journeys.*` code | Present — not a create wait |

No AWS/CDK, queue, or second host. `Journeys.UX` is out of increment.

## Human-gated holds (outside the Bolts)

| Hold | Who | Typical wait | Which Bolt | If it slips |
|------|-----|--------------|------------|-------------|
| Ledger / money-like dest clock | Human | Review of B2 AC and clock table | B2 (blocks B3 consume of C2) | Do not start B3 expire hops until the clock is accepted; webhook Calculate/Award could theoretically proceed but this plan keeps B3 together |
| Auth / tenant isolation | Human | Review of tenant-scoped config + payload | B3 | Do not merge webhook until tenant isolation AC4.1.1 is accepted |
| Merge to `main` / release | Human | After increment-close verify | After B3 | Agents never merge or release; work stays on the feature branch |
| Auth0 tenant `"hayward"` | Human | Out of increment | None | Do not edit |

Exact host/appsettings key for “webhook throw is non-fatal” is an open Construction name, not an external team (Contract Design open question).

## Slip plan

If B2’s human ledger gate is late, park B3’s expire-on-process and do not invent a second dest clock in U3. Do not start a parallel webhook Bolt that edits `EventService` in this session. B1 may already be done and does not wait on the ledger gate.
