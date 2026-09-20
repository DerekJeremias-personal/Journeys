# Units Generation Questions

Intent `mvp-engine-closeout`. Topology only — Delivery Planning chooses Bolt order. One Linear issue per unit after this stage is approved; no `Journeys.*` code until then. Sources: `components.md`, `decisions.md`, `requirements.md`, `stories.md`, approved plan suggested boundaries.

## Proposed plan (defaults if you accept the recommended options)

- **Five units**, one per approved-plan task: hydrate, earn-date cascade, expire-on-process, webhook, docs/graph.
- **Kinds:** four `library` (Core seams inside the existing API) + one `spec` (meaning docs / path-map). Not `service` (no new host) and not `ui`.
- **Topology:** expire-on-process depends on cascade (hops inside bring-current use earn-date math). Hydrate and webhook are independent of ledgers. Docs depends on the four code units (same change set). No recommended build order here.

## Q1. How many units?

The approved plan suggested five boundaries (four engine seams + docs). Stories are US1.1–US5.1. Linear will get one issue per unit.

A. Five units — hydrate, cascade, expire-on-process, webhook, docs/graph (recommended; matches the plan and stories)
B. Four units — fold docs into the last code unit (plan allowed this if we want fewer Linear issues)
C. One unit covering all four seams plus docs
X. Other (please specify)

[Answer]: B. Four units — fold docs into the last code unit (2026-09-19, **Mode:** Guide me)

## Q2. What is each unit?

These land in the existing modular monolith. A `service` unit would imply a new deployable. A `library` unit is Core/DTO/Notification code with no new host.

A. Four `library` units + one `spec` docs unit (recommended)
B. Four `service` units (treat each seam as its own deployable)
C. All `library`, including docs
X. Other (please specify)

[Answer]: B. Four `service` units (treat each seam as its own deployable) (2026-09-19, **Mode:** Guide me)

## Q2b. Confirm “service” vs the existing API

Q2-B means each seam is its own deployed executable. Affirmed practice and Domain Design keep one `Journeys.API` host — no new process, no AWS/CDK this increment.

A. Keep four `library` units inside the existing API (recommended; matches Q1 and the monolith)
B. I meant independently deployable services — new hosts this increment
X. Other (please specify)

[Answer]: A. Keep four `library` units inside the existing API (2026-09-19, **Mode:** Guide me; supersedes Q2-B)

## Q3. What depends on what?

Expire-on-process runs cascade hops inside bring-current. Hydrate does not need ledgers. Webhook does not need PAT math. Docs must stay in the same change as the seams.

A. Expire-on-process depends on cascade; hydrate and webhook have no unit dependencies; docs depends on all four code units (recommended)
B. No edges — all five independent
C. Single chain: hydrate → cascade → expire → webhook → docs
X. Other (please specify)

[Answer]: A. Expire-on-process depends on cascade; hydrate and webhook have no unit dependencies; docs rides on the webhook unit (2026-09-19, **Mode:** Guide me)

## Q4. How do they deploy?

A. Embedded in the existing `Journeys.API` modular monolith — no new process (recommended)
B. Independently deployable services
X. Other (please specify)

[Answer]: A. Embedded in the existing `Journeys.API` modular monolith — no new process (2026-09-19, **Mode:** Guide me)

## Consolidated Summary Confirmation

Does this all look correct before I generate the unit DAG?

- Looks correct
- Request changes

[Answer]: Looks correct
