<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
<!-- example: 2026-05-29T10:14:32Z — chose REST over GraphQL; the consuming team only needs CRUD, revisit if subscriptions land -->

- 2026-09-19T15:46:00Z — Execute user-stories: complex ledger/webhook logic and two personas, even though Journeys.UX is out of increment. Stories are ProcessEvent-observable, not screens.
- 2026-09-19T16:08:00Z — Mob objections folded without round 2: operator so-that on US3.1, ProcessEvent as When, award loop honors IsAwarded (AC4.1.9), split clock/error/MCP Thens.

## Deviations
<!-- example: 2026-05-29T10:14:32Z — skipped the optional caching layer the stage prose suggested; the dataset is small enough that it adds risk -->

- 2026-09-19T16:08:00Z — Advisory review READY with R-01 (taxonomic RuleState) and R-02 (missing-EarnDate last-resort clock) left for the human at the gate; no rewrite this pass.

## Tradeoffs
<!-- example: 2026-05-29T10:14:32Z — picked TDD over BDD this run; the team is unit-first and the domain is well-understood -->

- 2026-09-19T16:08:00Z — Split US2.1 and US4.1 into more ACs so QA can automate, rather than keeping bundled Thens that lock the wrong existing tests.

## Open questions
<!-- example: 2026-05-29T10:14:32Z — confirm the retention window with compliance before the next stage hardens the schema -->
