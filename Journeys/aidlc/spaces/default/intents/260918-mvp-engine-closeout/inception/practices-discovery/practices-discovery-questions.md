# Practices Discovery Questions

Intent `mvp-engine-closeout`. Brownfield re-run. Ask only what the lead draft and independent reviews could not establish. Already-affirmed items (capability ids, human merge, Core+DTO write path, verify script, no terraform/Databricks, ledger/auth/tenant gates, Linear one-issue-per-unit) are not re-asked.

## Q1. Feature-branch lifetime

We saw `feat/*` branches that last longer than a day or two. How should this team treat branch lifetime going forward? This decides how we describe Way of Working after affirmation.

A. Keep short-lived branches (about 1–2 days) and squash to `main`
B. Accept longer `feat/*` branches when a slice needs more than a couple of days; still squash to `main`
C. Something else we already do — describe it
X. Other (please specify)

[Answer]: B. Accept longer `feat/*` branches when a slice needs more than a couple of days; still squash to `main` (2026-09-19, **Mode:** chat)

## Q2. Thin end-to-end slice first?

Build a thin end-to-end slice first? A walking skeleton is a minimal version that runs the whole way through, built first to prove the pieces connect before the real features go in. Process-event already goes through API → Core → DTO. Should this increment still start with a new thin slice Bolt, or skip that ceremony?

A. Skip a second skeleton ceremony — the API + Core spine already exists
B. Still run a thin process-event slice as the first construction Bolt
X. Other (please specify)

[Answer]: A. Skip a second skeleton ceremony — the API + Core spine already exists (2026-09-19, **Mode:** chat)

## Q3. Test writing order for this increment

The approved closeout plan writes failing tests first on the four engine seams. The existing suite is mostly tests written after the code. Which durable methodology should we affirm?

A. tdd — failing test, then implementation, on these seams
B. custom — TDD on new closeout seams; existing historical tests stay test-after
C. test-after — implement the layer, then write that layer's tests
X. Other (please specify)

[Answer]: B. custom — TDD on new closeout seams; existing historical tests stay test-after (2026-09-19, **Mode:** chat)

## Q4. Coverage floor

Coverlet is in the test project but there is no in-repo coverage number. Org default for classic is 80%. What should we affirm?

A. No in-repo floor — keep the existing suite green and run `scripts/aidlc-agent-verify-sensor.ps1` (with `-RunTests` when tests change)
B. Affirm an 80% line-coverage floor for this solution
X. Other (please specify)

[Answer]: A. No in-repo floor — keep the existing suite green and run `scripts/aidlc-agent-verify-sensor.ps1` (with `-RunTests` when tests change) (2026-09-19, **Mode:** chat)

## Q5. Where releases actually go

There is no CI or deploy pipeline in this Journeys tree. Staging/prod may live elsewhere. Who ships, and where?

A. This tree has no CD — humans merge to `main`; deploy/release stays human-gated outside this folder
B. Deploy-on-merge to staging exists in another repo — I will name where
C. We do not have staging; production is a separate human approval
X. Other (please specify)

[Answer]: A. This tree has no CD — humans merge to `main`; deploy/release stays human-gated outside this folder (2026-09-19, **Mode:** chat)

## Q6. Security scanning location

No SAST, DAST, secret scan, or NuGet audit is configured in this tree. Secrets are meant to stay in user-secrets / env / gitignored local config. Where do scanners live?

A. Scanning is absent here — do not invent in-tree scanners this increment
B. Scanning lives in another repo or org pipeline — I will name it
X. Other (please specify)

[Answer]: A. Scanning is absent here — do not invent in-tree scanners this increment (2026-09-19, **Mode:** chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
