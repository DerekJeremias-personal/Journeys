# Delivery Planning Questions

Intent `mvp-engine-closeout`. A **Bolt** is one Construction pass over a slice of work that ends in something that runs (one or more units, a done check, and a confidence hypothesis). Units Generation already fixed the DAG: expire-on-process (`u3-expire-on-process`) depends on earn-date cascade (`u2-earn-date-cascade`). Linear issues JOU-1–JOU-4 map those units.

**Already decided (not re-asked):** skip a second walking-skeleton ceremony; four `library` units in `Journeys.API`; docs/graph increment-close on U4; no new AWS/host; humans merge/release; ledger/auth/tenant human-gated.

**Sources:** `requirements.md`, `stories.md`, `components.md`, `unit-of-work.md`, `unit-of-work-dependency.md`, `unit-of-work-story-map.md`, `contract-summary.md`, `team-practices.md`.

## Proposed plan (defaults if you accept the recommended options)

- **Heuristic:** mix — cheap hydrate first, then risk-first ledger cascade, then expire + webhook together because both touch `EventService`.
- **No formal WSJF scores.** Rationale names the mix; no spreadsheet.
- **Three Bolts:** `{u1-tree-hydrate}`, `{u2-earn-date-cascade}`, `{u3-expire-on-process + u4-notification-webhook}`.
- **Serial** in this session (classic; no Team Formation). AI developer owns every Bolt.
- **External holds:** none in-tree. Human merge/release and ledger/auth/tenant gates sit outside the Bolts.

## Q1. What do we build first?

This picks the path through the DAG. A thin end-to-end first Bolt was already declined (affirmed: skip a second skeleton). Units review also said `EventService` lock/bring-current and `INotificationService` host-copy are one shared edit site.

A. Mix — hydrate first (cheap, independent), then cascade (ledger risk), then expire + webhook in one Bolt (shared `EventService`) (recommended)
B. Risk-first — cascade first, then expire + webhook, hydrate whenever
C. Value-first — webhook first, then hydrate, then cascade, then expire
D. Thin end-to-end first Bolt anyway (reopen the skeleton)
X. Other (please specify)

[Answer]: A. Mix — hydrate first (cheap, independent), then cascade (ledger risk), then expire + webhook in one Bolt (shared `EventService`) (2026-09-19, **Mode:** Guide me)

## Q2. Formal ranking scores?

Weighted Shortest Job First (WSJF) scores value + urgency + risk-reduction against size. We can name a heuristic without inventing numbers.

A. No formal scores — write the mix heuristic in the rationale (recommended)
B. WSJF with equal weight on value, urgency, and risk
C. WSJF with extra weight on ledger/risk
X. Other (please specify)

[Answer]: A. No formal scores — write the mix heuristic in the rationale (2026-09-19, **Mode:** Guide me)

## Q3. How big is one Bolt?

A. Three Bolts — `{u1-tree-hydrate}`, `{u2-earn-date-cascade}`, `{u3-expire-on-process + u4-notification-webhook}` (recommended; shared `EventService`)
B. Four Bolts — one unit each; expire after cascade; webhook after expire so `EventService` is not edited in two Bolts at once
C. One Bolt — all four units
D. Two Bolts — `{u1 + u2}`, `{u3 + u4}`
X. Other (please specify)

[Answer]: A. Three Bolts — `{u1-tree-hydrate}`, `{u2-earn-date-cascade}`, `{u3-expire-on-process + u4-notification-webhook}` (2026-09-19, **Mode:** Guide me)

## Q4. Serial or overlapping Bolts?

Classic skipped Team Formation. Construction in this chat is one walk. Parallel sets in the DAG (`u1` with `u2`) do not require two Bolts at once.

A. Serial — one Bolt after another in this session (recommended)
B. Overlap hydrate with cascade (same person still; no second mob)
X. Other (please specify)

[Answer]: A. Serial — one Bolt after another in this session (2026-09-19, **Mode:** Guide me)

## Q5. Anything outside this team that can hold a Bolt?

A. Nothing in-tree — human merge/release and ledger/auth/tenant gates only; tenant webhook URL already exists on `NotificationConfig` (recommended)
B. Named external wait (API, data window, another team) — say who, how long, which Bolt
X. Other (please specify)

[Answer]: A. Nothing in-tree — human merge/release and ledger/auth/tenant gates only; tenant webhook URL already exists on `NotificationConfig` (2026-09-19, **Mode:** Guide me)

## Q6. What worries you most so we tackle it early?

A. Dest-clock math plus the shared `EventService` edit (recommended)
B. Webhook throw / tenant URL / payload hygiene
C. Hydrate collect missing child or nav rules
X. Other (please specify)

[Answer]: A. Dest-clock math plus the shared `EventService` edit (2026-09-19, **Mode:** Guide me)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
