# Design: AI-DLC harness closeout-friction patches

**Date:** 2026-09-25  
**Status:** Approved (human 2026-09-25, chat)  
**Scope:** Surgical patches to the Cursor AI-DLC harness in this repo so Classic no longer fails the way MVP engine closeout failed: gitignored build output as source, Plan Approval phrase matching, missing `runtime-graph.json`, and `review-brief` not loaded. Plus a refresh script/doc so Backend and other solutions can take the same harness before an upstream release exists.  
**Depends on:** closeout intent `260918-mvp-engine-closeout` retrospective (audit shard refusals; RFC #662; `.vs` bind). Not a Journeys product capability.  
**Placement authority:** `AGENTS.md` (humans merge/release). Harness files live under `.cursor/`. Product meaning stays in `docs/product/` — this increment does not add capability ids or graph nodes.  
**If this spec and shipped AI-DLC disagree on stage sequence / gates:** AI-DLC wins, except the five listed matcher/exclude/CLI bugs, which this spec changes on purpose.  
**If this spec and an upstream AI-DLC release disagree after merge:** prefer the upstream release; keep the Journeys refresh script as the way other local repos catch up.

This is **not** product closeout (JOU-1–JOU-4). It does not finish the open Build and Test gate on `260918-mvp-engine-closeout`.

---

## 1. Problem and goals

Classic on Cursor delivered the four engine units, then spent most remaining human time on harness refusals: source walk included `bin`/`obj`/`.vs`/`.tmp-probe`; Plan Approval required an exact minted phrase and failed `Approve` / `1`; `learnings surface` died because `runtime-graph.json` is gitignored; `aidlc engine review-brief review` printed `does not export main` because `loadDelegate` never imports `aidlc-review-brief.ts`.

The same harness should work in Backend and later solutions without waiting on a release, and the same diffs should be upstreamable.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Build output is not source** | Adding or deleting files under `bin/`, `obj/`, `.vs/`, or `.tmp-probe/` does not move `workspaceSourceFingerprint` and does not create RFC #662 unclaimed paths |
| G2 | **Plan Approval accepts the approve-side meaning** | Live receipt accepts minted labels, `1`/`2`, `Approve`, `Approve Plan`, and `I Approve Plan`, case-insensitive. `yes` / `lgtm` / `ok` still fail. Session + challenge id still bind |
| G3 | **Learnings can surface** | `runtime-graph.json` is not gitignored; after one orchestrate rebuild the file can be committed and `learnings surface` / `runtime summary` can read it |
| G4 | **review-brief CLI works** | `aidlc engine review-brief review` loads `aidlc-review-brief.ts` `main(argv)` instead of `does not export main` |
| G5 | **Other repos can refresh** | `scripts/aidlc-refresh-harness.ps1 -Target <path>` plus `docs/developer/aidlc-harness-refresh.md` copy harness-owned trees and refuse to clobber target memory, intents, or `.cursor/mcp.json` |
| G6 | **Upstream path** | The five tool/gitignore diffs are listed as the upstream PR body; humans open/merge that PR |

### Non-goals

- Wrapper/plugin overlay that leaves stock matchers in place
- Rebuild-on-demand graph as the primary design (human chose commit-the-file)
- Accepting `yes`, `lgtm`, `ok`, or `approved` as Plan Approval
- Changing Journeys onion product code or Linear JOU-1–JOU-4
- Finishing `260918-mvp-engine-closeout` Build and Test / CI Pipeline
- Agent merge to `main` or publishing the upstream release
- New exclude schema on `.aidlc-source-paths.json` (stays include-only)
- Copying a target repo’s `aidlc/spaces/*/memory` or intents onto this repo

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Approach | Surgical patches in `.cursor/tools` (and `.gitignore`, script, developer doc, conductor notes) |
| Landing | This repo first, then the same diffs as an upstream AI-DLC PR |
| Share to Backend | Script **and** `docs/developer/aidlc-harness-refresh.md` |
| Source excludes | Hard-exclude names: `bin`, `obj`, `.vs`, `.tmp-probe` (same mechanism as `node_modules`) |
| Plan Approval | Semantic pair, case-insensitive; session + challenge id unchanged; break-glass override unchanged |
| runtime-graph | Stop gitignoring `aidlc/spaces/*/intents/*/runtime-graph.json`; commit after rebuild |
| review-brief | Add `TOOLS.reviewBrief` to `loadDelegate` in `aidlc.ts`; do not write a second brief tool |
| Conductor notes | Short ALWAYS/NEVER in `project.md` / Journeys AIDLC skill |
| Execution | `/aidlc classic` after `execute` + intent name. Superpowers SDD void |
| Commit of this spec | Human only |

---

## 3. Components

### 3.1 Source fingerprint (`aidlc-lib.ts`)

Add `bin`, `obj`, `.vs`, `.tmp-probe` to `SOURCE_FINGERPRINT_HARD_EXCLUDED_NAMES`. They are excluded at every depth in Git and filesystem modes. Real source must not live under those directory names; if a future app tracks source in a folder literally named `bin`, that is out of increment (same rule as `node_modules`).

`.aidlc-source-paths.json` remains `{"version":1,"paths":[...]}` include-only.

### 3.2 Plan Approval receipt (`aidlc-testing-posture.ts` live path)

`offeredPlanApprovalChoice` (and any twin that feeds `recordPlanApprovalHumanResponse`) after `stripRecommendedDecorator`:

1. Collapse internal whitespace; compare case-insensitively.
2. Exact minted option labels still win.
3. `1` → first option (Approve Plan side); `2` → second (Request Changes).
4. Approve-side synonyms: `approve`, `approve plan`, `i approve plan`.
5. Reject-side synonym: `request changes`.
6. If `requireExactOptionLabels` is set, still apply (1)–(5). The closeout bug was exact-label mode dropping `1`/`Approve`.

Do not change session/challenge identity. Unreadable source that is **not** in the new exclude list still uses `Override Plan Approval: <reason>`.

### 3.3 runtime-graph.json

Remove this ignore rule:

```
aidlc/spaces/*/intents/*/runtime-graph.json
```

After the next `orchestrate` rebuild (already happens when the graph is missing or stale), commit the file. Fresh clones: run one `aidlc` / `next` so the file appears, then commit. Learnings/session-cost fail until that first rebuild — documented, not a silent empty success.

### 3.4 review-brief delegate (`aidlc.ts`)

`aidlc-review-brief.ts` already exports `main(argv)`. `loadDelegate` has no `case TOOLS.reviewBrief`. Add:

`case TOOLS.reviewBrief: return import("./aidlc-review-brief.ts");`

### 3.5 Refresh script and doc

`scripts/aidlc-refresh-harness.ps1 -Target <absolute-or-relative-path>`

Copies harness-owned trees from this repo into the target, at least:

- `.cursor/tools`
- `.cursor/skills/aidlc*`
- `.cursor/aidlc-common`
- `.cursor/agents/aidlc-*`
- `.cursor/sensors/aidlc-*`

Refuses (non-zero, no write) when:

- `-Target` is missing, is this repo, or is not a directory
- The copy would overwrite the target’s `aidlc/spaces/*/memory`, `aidlc/spaces/*/intents`, or `.cursor/mcp.json`

`docs/developer/aidlc-harness-refresh.md` lists the same trees, the refuse list, and `aidlc --doctor` in the target. Add a one-line link from `docs/developer/index.md`.

The script/doc may stay Journeys-owned if upstream does not want PowerShell.

### 3.6 Conductor notes

Add to `aidlc/spaces/default/memory/project.md` (and the Journeys AIDLC skill if it already states verify):

- Prefer the minted Plan Approval label; the engine now also accepts the G2 synonyms.
- Only `StrReplace` the `[Answer]:` line in a questions file (do not `Write` the whole file).
- Construction verify: `aidlc-agent-verify-sensor.ps1 -Files` from the onion folder (`Journeys/` is not the git root). Do not pass `Journeys.Tests` paths unless you intend the full test project.

---

## 4. Error handling

| Case | Behavior |
|------|----------|
| Source still unreadable after excludes | Existing Plan Approval unbindable + override |
| Human says `yes` / `lgtm` | Receipt refuses (null mapper) |
| Refresh target unsafe | Exit non-zero; no partial copy of refused trees |
| Graph file absent on clone | Orchestrate rebuilds; learnings fail until the file exists; doc says so |
| review-brief still missing `main` | Out of increment if `loadDelegate` is wired and export remains |

---

## 5. Testing

No second test project. Use the existing harness TypeScript test runner if present; otherwise a small file beside the tools.

| Case | Expected |
|------|----------|
| Create a file under `bin/`, `obj/`, `.vs/`, `.tmp-probe/` | Fingerprint unchanged |
| Mapper `Approve`, `APPROVE PLAN`, `I Approve Plan`, `1` | Approve-side |
| Mapper `2`, `Request Changes` | Reject-side |
| Mapper `yes`, `lgtm` | Null |
| `aidlc engine review-brief review` | Does not print `does not export main` |
| Refresh dry-run / temp target | Copies tools; does not write planted memory or `mcp.json` |

---

## 6. Lifecycle (execution)

```
Human approves this spec + matching plan
  → execute <intent-name>
  → /aidlc classic (ingest spec/plan; do not re-brainstorm)
  → Linear units before harness edits if units-generation requires it
  → Construction patches
  → Human commits / optional upstream PR
```

Agents do not merge to `main`. Agents do not open the upstream PR unless the human asks in that message.

---

## 7. Follow-on (not this increment)

- Upstream AI-DLC release that includes these diffs (human merge)
- Rebuild-on-demand graph if committed graphs become too noisy
- Exclude schema for `.aidlc-source-paths.json` if a repo needs custom generated-output names
- Closing `260918-mvp-engine-closeout` Build and Test / CI Pipeline / Linear complete
