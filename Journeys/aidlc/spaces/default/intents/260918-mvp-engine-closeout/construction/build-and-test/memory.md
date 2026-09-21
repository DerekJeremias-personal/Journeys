<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
<!-- example: 2026-05-29T10:14:32Z — chose REST over GraphQL; the consuming team only needs CRUD, revisit if subscriptions land -->
- 2026-09-21T18:50:00Z — Standard strategy means integration oracles plus no load suite; NFR1–7 are tenant, secrets, at-least-once, ledger gate, lock, throw-switch, and test posture, not latency numbers.

## Deviations
<!-- example: 2026-05-29T10:14:32Z — skipped the optional caching layer the stage prose suggested; the dataset is small enough that it adds risk -->
- 2026-09-21T18:50:00Z — Construction verify ran with `-Files` on closeout product and docs paths because this folder is not the git root; omitted `Journeys.Tests` paths so the script would not run the whole suite.
- 2026-09-21T18:52:00Z — Human accepted the historical `UserPointsTests` failure (17 `user1` / null-arg fixtures) and asked to continue to the stage checkpoint.

## Tradeoffs
<!-- example: 2026-05-29T10:14:32Z — picked TDD over BDD this run; the team is unit-first and the domain is well-understood -->
- 2026-09-21T18:50:00Z — Used the four closeout filters plus named historical filters instead of a bare `dotnet test`; full-suite backend fixtures are not this increment’s oracle.

## Open questions
<!-- example: 2026-05-29T10:14:32Z — confirm the retention window with compliance before the next stage hardens the schema -->
- 2026-09-21T18:50:00Z — Whether a later intent should seed `user1` and restore null-account asserts in `UserPointsTests` as its own slice.
