# Team allocation — MVP engine closeout

A **Bolt** is one Construction pass over a slice of work that ends in something that runs. A **mob** here is who owns that pass. Classic skipped Team Formation, so there is no Program Board of several human teams — one AI developer owns every Bolt in this session.

**Sources:** `bolt-plan.md`, `team-practices.md`, Delivery Planning Q4.

## Ownership model

- All Bolts: `aidlc-developer-agent` (AI), inline in this chat.
- Reviewers on later Construction stages stay the shipped ensemble (`architecture-reviewer`, `product-lead`, plus `quality` / `devsecops` where those stages dispatch them). Delivery Planning itself has no reviewer.
- Humans approve non-trivial design, merge, and release. Auth, ledger/money-like outcomes, and tenant isolation stay human-gated (B2 ledger clock is the first of those).
- No second mob and no overlapping Bolts in this session (Q4 serial).

## Assignments

| Bolt | Units | Mob | Human gate |
|------|-------|-----|------------|
| B1 Tree hydrate | `u1-tree-hydrate` | `aidlc-developer-agent` | Claim JOU-1 before code; merge/release human |
| B2 Earn-date cascade | `u2-earn-date-cascade` | `aidlc-developer-agent` | Claim JOU-2; ledger clock human-gated before merge |
| B3 Expire + webhook | `u3-expire-on-process`, `u4-notification-webhook` | `aidlc-developer-agent` | Claim JOU-3 and JOU-4; tenant isolation + webhook throw switch; docs increment-close |

## Program Board

Not used. One owner, three serial Bolts. Parallel DAG sets (`u1` with `u2`) stay unused so `EventService` is edited once (B3) and the ledger clock lands before expire-on-process consumes it.
