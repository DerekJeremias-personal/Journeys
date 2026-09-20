<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
<!-- example: 2026-05-29T10:14:32Z — chose REST over GraphQL; the consuming team only needs CRUD, revisit if subscriptions land -->

- 2026-09-19T15:10:00Z — Requirements source is the approved spec at docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md. Plan is decomposition input only. Do not re-brainstorm G1–G4. Five clarifying questions cover only spec gaps (existing UtcNow rows, bring-current throw, missing dest PAT, adapter throw, no new lock).

## Deviations
<!-- example: 2026-05-29T10:14:32Z — skipped the optional caching layer the stage prose suggested; the dataset is small enough that it adds risk -->

- 2026-09-19T15:34:00Z — Advisory review READY with R-01 Major (dest-PAT load throw vs FR3.5). Left for the human at the gate; no lead rewrite this pass.

## Tradeoffs
<!-- example: 2026-05-29T10:14:32Z — picked TDD over BDD this run; the team is unit-first and the domain is well-understood -->

- 2026-09-19T15:33:00Z — Webhook throw fails ProcessEvent by default; a host/appsettings switch can make it non-fatal. Prefer fail-closed for money-adjacent sibling consistency unless operators opt in.
- 2026-09-19T15:33:00Z — ProcessEvent bring-current runs under existing TryLockAccount; GET/reconcile outer-lock cleanup deferred so this increment stays ProcessEvent-only.

## Open questions
<!-- example: 2026-05-29T10:14:32Z — confirm the retention window with compliance before the next stage hardens the schema -->

- 2026-09-19T15:33:00Z — Exact Q4 configuration key name is Construction, not a product capability.
