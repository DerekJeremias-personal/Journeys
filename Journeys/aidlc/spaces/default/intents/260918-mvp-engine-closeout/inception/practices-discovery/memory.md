<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
<!-- example: 2026-05-29T10:14:32Z — chose REST over GraphQL; the consuming team only needs CRUD, revisit if subscriptions land -->

- 2026-09-19T14:50:00Z — Chat interview Q1–Q6 plus Looks correct is the spoken practice set for this brownfield re-run. Longer feat/* when a slice needs it (still squash, humans merge); skip a second walking-skeleton ceremony because the API + Core spine already exists; Methodology custom (TDD on new closeout seams, historical tests stay test-after); no in-repo coverage floor; no CD in this tree; no in-tree scanners this increment.

## Deviations
<!-- example: 2026-05-29T10:14:32Z — skipped the optional caching layer the stage prose suggested; the dataset is small enough that it adds risk -->

- 2026-09-19T14:50:00Z — Quality asked to always pass -RunTests on engine Construction verifies. Interview Q4 chose suite-green plus -RunTests when tests change, so the stricter always-on-engine-seams rule was not written into team-practices.

## Tradeoffs
<!-- example: 2026-05-29T10:14:32Z — picked TDD over BDD this run; the team is unit-first and the domain is well-understood -->

- 2026-09-19T14:50:00Z — Affirmed Methodology custom instead of unlabeled tdd so later stages do not treat every historical Journeys.Tests case as red-first. Closeout units still write the failing test first.
- 2026-09-19T14:50:00Z — Kept no in-repo coverage floor and no CD/scanners in this tree instead of org classic 80% / deploy-on-merge / linter-in-CI. Those org defaults are not evidenced here and were not spoken as yes.

## Open questions
<!-- example: 2026-05-29T10:14:32Z — confirm the retention window with compliance before the next stage hardens the schema -->

- 2026-09-19T14:50:00Z — HintPath Backend.* binaries may be reviewed or pinned outside this tree; noted only, not a new rule.
