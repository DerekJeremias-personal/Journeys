# Build and Test questions

## Halt-and-ask — UserPointsTests historical failures

Build and Test failed: 17 `UserPointsTests` failed (`user1` missing; null-arg asserts). Root cause: pre-existing GET/reconcile fixtures, not the expire-on-process closeout. Candidate fix: seed `user1` and align null-account asserts in `UserPointsTests` — estimated impact — effort: half to one day; financial cost: none; risk: medium (touches historical expire GET/reconcile, not ProcessEvent order). Loop-backs used: 0/3. How would you like to proceed?

1. **Retry with fix** — Jump back to Code Generation, apply the `UserPointsTests` fixture repair (estimated impact — effort: half to one day; financial cost: none; risk: medium), re-run.
2. **Accept failure** — Keep the failure in `test-results.md` and continue to this stage’s approval.
3. **Abort** — Stop here; the workflow can resume later.

[Answer]: Accept failure

## Learnings — Anything to add for next time?

Anything to add for next time?

1. Nothing to add
2. Add a note

[Answer]: Nothing to add
