# SDD progress — Journeys.UX loyalty shell

Plan: `Journeys/docs/plans/2026-09-14-Journeys-ux-loyalty-shell.md`
Branch: `feat/journeys-ux-loyalty-shell`
(Do not confuse with the agentic-OS entries in `progress.md`.)

- Task 1: complete (no commit; review clean). Minor: `tsconfig.tsbuildinfo` not gitignored.
- Task 2: complete (no commit; review clean). 10/10 vitest. Minors: 2xx non-JSON success (plan-mandated); error body slice.
- Task 3: complete (no commit; review clean). 14/14 vitest. Minors: untested tenant/baseUrl/no-cred edges; wrap entities not asserted; whitespace userId.
- Task 4: complete (no commit; review Approved, spec ✅). 16/16 + tsc 0. Important/plan-mandated: `apiKeyLoginEnabled()` at NextAuth module load vs per-request form (brief snippet). Minors: Auth0 without tenant; no auth-flow tests; no sign-in error UX. Controller type fixes: session typeof guards; test tuple casts.
- Task 5: complete (no commit; review Approved, spec ✅). 16/16 + tsc 0. Minors: dual h1 Loyalty; flat nav; dual auth gate (allowed).
- Task 6: complete (no commit; review Approved, spec ✅). 21/21 + tsc 0. Important/brief-ambiguous: getSchemaByName collapses API errors to null (missing-schema copy). Minors: formatFetchError dup; extractEntities Entities/items untested; data! asserts.
- Task 7: complete (no commit; review Approved, spec ✅). Seven capabilities + proj-ux; ui-in-this-sln gone. Important leftover (out of file list): docs/product/taxonomies/constraints.md `no-ui-in-sln`.
- Task 8: complete (no commit; controller verified). docs-impact 0; graph-impact 0; npm test 21/21; sln build 0 via alternate -o (in-place sln build failed: running Journeys.API file lock). Step 4 human smoke not run.
- Final review: With fixes. Applied: Accounts fetch errors no longer look like missing-schema; constraints.md `no-ui-in-sln` rewritten. Left as plan trade-off: session copies apiKey/accessToken. After fixes: npm test 21/21, tsc 0.

## Roll-up minors for whole-branch review

- tsconfig.tsbuildinfo not gitignored
- apiKeyLoginEnabled evaluated at NextAuth module load (plan-mandated)
- getSchemaByName collapses fetch errors to missing-schema copy
- constraints.md `no-ui-in-sln` stale
- coding-standards.md still says Next.js UI is not in this solution (not in Task 7 file list)
