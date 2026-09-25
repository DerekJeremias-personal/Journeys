# AI-DLC harness closeout-friction Implementation Plan

> **Execution:** After approval, say execute and the intent name.
> `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not**
> use superpowers:subagent-driven-development. Linear unit issues land before
> any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Stop the Classic harness failures from MVP engine closeout (build output as source, Plan Approval phrase matching, missing `runtime-graph.json`, `review-brief` not loaded) and let Backend refresh the same harness via script + doc.

**Architecture:** Surgical patches in this repo’s `.cursor/tools` (same diffs for an upstream AI-DLC PR). Hard-exclude `bin`/`obj`/`.vs`/`.tmp-probe`. Loosen only the Plan Approval **receipt mapper**, not session/challenge identity. Stop gitignoring `runtime-graph.json`. Wire `TOOLS.reviewBrief` in `loadDelegate`. Add `scripts/aidlc-refresh-harness.ps1` and a developer page. Short conductor notes in `project.md`.

**Tech Stack:** TypeScript harness (`.cursor/tools`), PowerShell refresh script, Markdown developer docs. No `Journeys.*` product code.

**Spec:** `docs/specs/2026-09-25-aidlc-harness-closeout-friction-design.md`

## Global Constraints

- Do not invent capability ids. Do not add product graph nodes.
- Do not change Journeys onion product code or Linear JOU-1–JOU-4.
- Do not finish intent `260918-mvp-engine-closeout` Build and Test / CI in this plan.
- Do not accept `yes` / `lgtm` / `ok` as Plan Approval.
- Do not drop Plan Approval session + challenge-id binding.
- Do not add an exclude schema to `.aidlc-source-paths.json` (stays include-only).
- Refresh must not overwrite target `aidlc/spaces/*/memory`, `intents`, or `.cursor/mcp.json`.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Agents do not merge to `main`. Superpowers SDD is void.

## File map

| Path | Responsibility |
|------|----------------|
| `.cursor/tools/aidlc-lib.ts` | Add `bin`, `obj`, `.vs`, `.tmp-probe` to `SOURCE_FINGERPRINT_HARD_EXCLUDED_NAMES` |
| `.cursor/tools/aidlc-testing-posture.ts` | `offeredPlanApprovalChoice` semantic pair, case-insensitive |
| `.cursor/tools/aidlc.ts` | `loadDelegate` case for `TOOLS.reviewBrief` |
| `.gitignore` | Remove `runtime-graph.json` ignore |
| `scripts/aidlc-refresh-harness.ps1` | Copy harness-owned trees; refuse memory/intents/mcp |
| `docs/developer/aidlc-harness-refresh.md` | How to refresh Backend / other repos |
| `docs/developer/index.md` | Link the refresh page |
| `aidlc/spaces/default/memory/project.md` | Conductor ALWAYS/NEVER notes |
| `.agents/skills/aidlc-journeys/SKILL.md` | Verify `-Files` note if not already there |
| Harness test file (new, next to tools or existing runner) | Fingerprint + mapper + review-brief + refresh refuse |

---

### Task 1: Source walk excludes

**Files:**
- Modify: `.cursor/tools/aidlc-lib.ts`
- Create or modify: harness test for fingerprint excludes

**Interfaces:**
- Consumes: `SOURCE_FINGERPRINT_HARD_EXCLUDED_NAMES`
- Produces: unchanged fingerprint when files appear under excluded names

- [ ] **Step 1: Write a failing fingerprint test**

Create a temp file under each of `bin/`, `obj/`, `.vs/`, `.tmp-probe/`. Assert `workspaceSourceFingerprint` (or the listing used by RFC #662) does not change versus a baseline without those files.

- [ ] **Step 2: Add the four names to `SOURCE_FINGERPRINT_HARD_EXCLUDED_NAMES`**

Keep `node_modules` and the existing list. Do not add an exclude key to `.aidlc-source-paths.json`.

- [ ] **Step 3: Re-run the fingerprint test**

Must pass. A `dotnet build` that only writes `bin`/`obj` must not produce unclaimed application-source paths.

---

### Task 2: Plan Approval receipt mapper

**Files:**
- Modify: `.cursor/tools/aidlc-testing-posture.ts` (`offeredPlanApprovalChoice` and any twin that feeds `recordPlanApprovalHumanResponse`)
- Modify: harness tests for the mapper

**Interfaces:**
- Consumes: minted option labels, `stripRecommendedDecorator`
- Produces: `"Approve Plan"` \| `"Request Changes"` \| `null`

- [ ] **Step 1: Write failing mapper tests**

Cover: `Approve`, `APPROVE PLAN`, `I Approve Plan`, `1` → approve-side; `2` / `Request Changes` → reject; `yes` / `lgtm` → `null`. Case-insensitive; collapse extra spaces.

- [ ] **Step 2: Implement the semantic-pair mapper**

After decorator strip: case-insensitive minted labels; `1`/`2`; approve synonyms; `request changes`. Apply even when `requireExactOptionLabels` is true. Do not change session/challenge identity or override break-glass.

- [ ] **Step 3: Re-run mapper tests**

Must pass. Export the function if tests cannot reach it otherwise.

---

### Task 3: Commit `runtime-graph.json`

**Files:**
- Modify: `.gitignore` (remove `aidlc/spaces/*/intents/*/runtime-graph.json`)
- Modify: `docs/developer/aidlc-harness-refresh.md` (or the refresh doc in Task 5) — “run one `next` on a fresh clone, then commit the graph”

- [ ] **Step 1: Remove the ignore rule**

- [ ] **Step 2: Rebuild the graph**

Run the existing orchestrate/rebuild path so `aidlc/spaces/default/intents/<active>/runtime-graph.json` exists. Do not invent a second graph format.

- [ ] **Step 3: Confirm `aidlc engine learnings surface --slug build-and-test` no longer fails with `runtime-graph.json not found`**

If no active intent graph yet, document the one-`next` step and assert the ignore rule is gone (`git check-ignore` returns not-ignored).

---

### Task 4: review-brief `loadDelegate`

**Files:**
- Modify: `.cursor/tools/aidlc.ts` (`loadDelegate` switch)

- [ ] **Step 1: Reproduce**

`aidlc engine review-brief review` must currently print `does not export main` (or equivalent).

- [ ] **Step 2: Add `case TOOLS.reviewBrief: return import("./aidlc-review-brief.ts")`**

Do not add a second brief implementation. `main(argv)` already exists.

- [ ] **Step 3: Re-run the CLI**

Must not print `does not export main`. Usage/help or a review brief is acceptable.

---

### Task 5: Refresh script and developer doc

**Files:**
- Create: `scripts/aidlc-refresh-harness.ps1`
- Create: `docs/developer/aidlc-harness-refresh.md`
- Modify: `docs/developer/index.md`

- [ ] **Step 1: Write the script**

`-Target` required. Copy `.cursor/tools`, `.cursor/skills/aidlc*`, `.cursor/aidlc-common`, `.cursor/agents/aidlc-*`, `.cursor/sensors/aidlc-*`. Exit non-zero if target is missing, is this repo, or the copy would overwrite target `aidlc/spaces/*/memory`, `intents`, or `.cursor/mcp.json`.

- [ ] **Step 2: Write the doc and index link**

Same trees, refuse list, then `aidlc --doctor` in the target.

- [ ] **Step 3: Test refuse + copy**

Temp target with a planted `mcp.json` and `aidlc/spaces/default/memory/project.md`. After run: tools copied; planted files unchanged.

---

### Task 6: Conductor notes and upstream file list

**Files:**
- Modify: `aidlc/spaces/default/memory/project.md`
- Modify: `.agents/skills/aidlc-journeys/SKILL.md` (only if verify `-Files` is not already stated)
- Create (optional, in the intent or spec follow-on): a short “upstream PR file list” section in the refresh doc or plan

- [ ] **Step 1: Add ALWAYS/NEVER lines**

Minted label preferred; only `StrReplace` `[Answer]:`; verify with `-Files` from the onion folder; do not pass `Journeys.Tests` in `-Files` unless you intend the full suite.

- [ ] **Step 2: List the exact paths for the upstream PR**

`aidlc-lib.ts`, `aidlc-testing-posture.ts`, `aidlc.ts`, `.gitignore` policy note. Script/doc may stay Journeys-owned.

- [ ] **Step 3: Do not open the PR** unless the human asks in that message.

---

## Suggested Classic units (for units-generation)

| Unit | Maps to |
|------|---------|
| `u1-source-excludes` | Task 1 |
| `u2-plan-approval-mapper` | Task 2 |
| `u3-runtime-graph-gitignore` | Task 3 |
| `u4-review-brief-delegate` | Task 4 |
| `u5-harness-refresh` | Task 5 |
| `u6-conductor-notes` | Task 6 |

U3 and U6 are docs/config. U5 may depend on U1–U4 being in the tree you copy.

---

## After this plan

Say `execute` plus an intent name (for example `aidlc-harness-closeout-friction`). I will start `/aidlc classic` and ingest this spec + plan. I will not start Superpowers SDD. I will not patch `.cursor/tools` until that execute and Linear-before-code (if the workflow requires issues for these units).
