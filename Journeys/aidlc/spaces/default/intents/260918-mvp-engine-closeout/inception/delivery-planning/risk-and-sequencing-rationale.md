# Risk and sequencing rationale — MVP engine closeout

A **Bolt** is one Construction pass over a slice of work that ends in something that runs. Units Generation fixed topology only. This file is why we walk the DAG in this order.

**Heuristic:** mix (Delivery Planning Q1–Q2). No formal Weighted Shortest Job First (WSJF) scores — no spreadsheet of value + urgency + risk ÷ size. Named mix: cheap independent hydrate first, then risk-first ledger cascade, then expire + webhook together because they share `EventService`.

**Sources:** `delivery-planning-questions.md`, `unit-of-work-dependency.md`, `contract-summary.md`, Contract Design accepted risks R-01 / R-02.

## Heuristic

| Lens | Applied where |
|------|----------------|
| Cheap / independent first | B1 `u1-tree-hydrate` — no ledger, no `EventService`, no webhook |
| Risk-first (Reinertsen risk-reduction, not scored WSJF) | B2 `u2-earn-date-cascade` — dest-clock math is the early worry (Q6) and is human-gated |
| Shared-edit cohesion | B3 bundles `u3-expire-on-process` + `u4-notification-webhook` so lock/bring-current and `INotificationService` host-copy are one `EventService` edit |
| Walking skeleton | Skipped (affirmed). Not used as a first Bolt |

Cohn “risk or value first” is the named mix; SAFe WSJF numbers were declined (Q2 A).

## Bolt order

1. **B1** then **B2** then **B3**. Serial (Q4).
2. **DAG:** `u3` after `u2` is required. `u1` and `u4` have no edges. Putting `u1` first is allowed. Holding `u4` until B3 is economic, not topological — `u4` could have started with B1.
3. **Deviation to record:** we delay the webhook unit until after the ledger Bolt so this session never edits `EventService` in two Bolts and so bring-current already uses C2 hops when expire-on-process is proven. That is a value/risk mix, not a DAG break.

## Early risk

| Worry (Q6) | Where it is tackled |
|------------|---------------------|
| Dest-clock math | B2 first ledger Bolt; AC2.1.1–AC2.1.6; human gate |
| Shared `EventService` edit | B3 only; U3 order + U4 state copy in one pass |
| Contract Design R-01 (dest expiration unset when dest has neither end-date nor days) | Accepted on Approve; B2 tests AC2.1.5 |
| Contract Design R-02 (fail-the-event vs HTTP 400) | Existing ProcessEvent failure path; not a new public error contract |

Webhook throw / tenant URL / hydrate-collect misses are secondary; they land in B3 and B1 respectively after the clock is settled.

## What we declined

- Risk-first-only (cascade before hydrate) — loses a cheap independent pass and does not reduce ledger risk.
- Value-first webhook first — would touch `EventService` before U3 order is designed, and splits the shared edit.
- Thin end-to-end first Bolt — reopens the skipped skeleton.
- Four Bolts or one Bolt — four re-opens the shared `EventService` site; one Bolt hides B2’s ledger gate.
- Formal WSJF weights — four units do not need invented scores.
