# SDD progress — Journeys.UX Campaigns IA

Plan: `Journeys/docs/plans/2026-09-18-Journeys-ux-campaigns-ia.md`
Spec: `Journeys/docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md`
Branch: `feat/journeys-ux-loyalty-shell` (work in place; do not commit unless user asks)

- Task 1: complete (no commit; review approved). Minor: campaign action method/path combos lack direct tests (mapper+fetch covered).
- Task 2: complete (no commit; uniqueness+controller tests). Reviewer leftover: docs-impact deferred to Task 8; restore TOCTOU not changed (adapter uniqueness is out of slice).
- Task 3: complete (no commit; review approved).
- Task 4: complete (no commit; re-review approved). Minor: inspector/resume/live-route lack component tests.
- Task 5: BLOCKED on full EXP lift (wizard imports). Focused /new + hydrate landed (93 tests). Resume Task 5 after Task 6.

- Task 6: complete (no commit; re-review approved). Minors: new-draft status select still offers Live/Pause/Archive (helper forces draft); validateCampaign unused (EXP client ValidationSummary); taxonomy stubs; types split. ⚠️ tsc pre-existing auth.ts + loyalty-model.test.ts confirmed allowed. ⚠️ [id]/agent non-Live redirect is Task 4, still present.

- Task 5: complete (no commit; re-review approved). Minors: dead link-draft banner; no FormProvider RTL reset test; lastHydratedIdRef skip. ⚠️ live SSE hydrate deferred to Task 8 browser pass. Empty-draft startDate + hydrateGeneration + linked GET skip fixed in review loop.

- Task 7: complete (no commit; review approved after dropping archived detailHref). Minors: kebab Edit/Duplicate no-ops on archive; versions 404 shown as error; journeys-ux.md table split (Task 8). Restore not clicked (empty archive list).

- Task 8: complete (no commit; DONE_WITH_CONCERNS). Docs/graph updated; route table fixed; `journeys` on UX path-map; no `IMPLEMENTED_AS` `proj-ux` on `campaigns`. docs-impact/graph-impact OK. npm test 167 pass. agent-verify Debug build locked by running Journeys.API (isolated 14 tests pass). Browser: list/wizard/inspector/live-agent redirect/archive empty/versions error/SSE turn OK; Duplicate/Unpublish/Publish-confirm/Resume-link incomplete (getall Live-only, save 500, conversations list empty). Restore not clicked.

- Task 8: docs/graph done (path-map journeys added; route table fixed; no proj-ux IMPLEMENTED_AS). Spec status reverted to Draft — browser pass failed Duplicate (no toast/new card), Unpublish (save 500, stayed Live), Resume (no list link; empty history on ?conversationId=). agent-verify Debug locked by running API; isolated copy/restore tests 14/14; npm test 167. Not closing as Approved.

- Task 8 follow-up: complete (no commit; re-review approved). Spec stays Draft. List merges live+draft+pause; Unpublish omits journey; conversationId both casings. Leftovers: restart Journeys.API for POST copy; Resume hidden while conversations items:[]; Debug agent-verify locks on running API.
