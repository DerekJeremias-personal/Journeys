# SDD progress — Journeys.UX accounts detail

Plan: `Journeys/docs/plans/2026-09-18-Journeys-ux-accounts-detail.md`
Spec: `Journeys/docs/specs/2026-09-18-Journeys-ux-accounts-detail-design.md`
Workspace: in-place on `feat/journeys-ux-loyalty-shell` (dirty tree; campaigns IA already present). AI-DLC skipped by human. No commits unless asked.

## Task 1: Allowlist + audit + queryData params — APPROVED
- Implementer: DONE_WITH_CONCERNS (`ux-accounts-detail-task-1-report.md`)
- Reviewer: APPROVED (minor test-coverage nits only; no fix pass)
- Notes: 183/183 tests. Pre-existing tsc in `auth.ts`, `campaigns-client.tsx`, `loyalty-model.test.ts`. Accounts page stubbed for Task 2.

## Task 2: Identifiers + dynamic-data + accounts list — APPROVED
- Implementer: DONE_WITH_CONCERNS (`ux-accounts-detail-task-2-report.md`)
- Reviewer: APPROVED (minor identifier-test coverage nit; deferred `dynamic-entity-details` accepted)
- Notes: 196/196 tests. List empty state + DynamicDataTable + no builder/data-explorer. Suspense for useSearchParams.

## Task 3: Read-only account detail — APPROVED
- Implementer: DONE_WITH_CONCERNS (`ux-accounts-detail-task-3-report.md`)
- Reviewer: APPROVED (journey tree on campaign list accepted as minor payload side effect)

## Task 4: Deposit / spend / expire — APPROVED
- Implementer: DONE_WITH_CONCERNS (`ux-accounts-detail-task-4-report.md`)
- Reviewer: APPROVED

## Task 5: Assign / remove journey + manage tier — APPROVED
- Implementer: DONE, then fix pass for Live-only tier campaigns
- Reviewer: NEEDS_CHANGES then APPROVED after Live filter (`select-tier-campaigns.ts`)

## Task 6: Docs + graph + browser pass — APPROVED
- Implementer: DONE_WITH_CONCERNS (no Live schema in local tenant; builder 404 added)
- Reviewer: APPROVED
- Whole-branch: With fixes → three Important issues fixed and re-approved
- Controller browser: empty-state sentence, `/builder` 404, unresolved id has no Actions

## Done
Ready for human commit. No agent commits/PRs. Live deposit/spend/expire and journey/tier writes still need a human smoke test against a tenant with Live `LoyaltyAccountDetails`.
