# Design: Linear as an AI-DLC unit projection

**Date:** 2026-09-14  
**Status:** Approved (BishopLabs Journeys team; engine `complete` maps to Linear **In Review**)  
**Scope:** Phase 2 adapter. Project AI-DLC units onto Linear so humans can review stories before construction, watch progress, and see Dev Complete bound to a commit.  
**Depends on:** [2026-09-13 Journeys agentic construction OS](2026-09-13-Journeys-agentic-os-design.md) (AI-DLC is the execution kernel; this spec does not replace it).  
**Placement authority:** same as the 2026-09-13 spec. Product meaning stays in `docs/product/` and `AGENTS.md`.  
**If this spec and AI-DLC disagree on stage sequence / gates:** AI-DLC wins, unless `AGENTS.md` human-only rules apply (merge, auth, ledger, tenant isolation).  
**If this spec and Linear disagree on story text after claim:** the unit file wins.

This is the Linear adapter the 2026-09-13 spec deferred (v1 non-goal: “Linear or any ticket poller”). It is **not** a ticket poller. Agents still claim AI-DLC units.

---

## 1. Problem and goals

AI-DLC units and `stories.md` in the intent record are canon for work. That is the right kernel. It is a poor human review surface: you cannot scan upcoming stories, business context, and exit criteria on a board before agents run, nor see Dev Complete tied to a commit while construction proceeds.

Linear is suitable as that board. It is not suitable as the work source, the gate engine, or a second conductor (Linear Agent coding sessions).

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Pre-execution review board** | After units are generated, one Linear issue exists per unit in Todo, with title, business context, and exit criteria copied from the unit |
| G2 | **Review edits land in canon** | Before claim, title / description / AC edited on Linear are written back into the unit file. Construction does not start until that pull-back has run |
| G3 | **Engine stays canon after claim** | Claim freezes unit text. Later Linear description edits do not change what the agent implements. Status drags never claim, unclaim, or complete a unit |
| G4 | **Live progress** | While a unit is in construction, the adapter posts comments (files, tests, blockers) — not a full diff dump |
| G5 | **Dev Complete + commit** | When gates pass and a commit exists, the issue moves to Dev Complete and the commit is linked (GitHub magic words, or SHA + commit URL attachment) |
| G6 | **Human merge veto** | Done is after you merge. Agents do not merge to `main` |

### Non-goals

- Agents polling Linear for work (`list_issues` as the queue)
- Linear Agent coding sessions as implementer
- Bidirectional status (Linear In Progress does not claim a unit)
- Replacing AI-DLC units / `stories.md` as source of work
- Playwright, PR admission CI, agent merge, per-tenant Auth0, pay-per-use billing (still later, per 2026-09-13)
- Webhook service that patches unit files on every Linear keystroke
- Standing up Linear as a second product graph
- Forking shipped AI-DLC sensor dispatch (`aidlc-sensor-*.ts` must stay `aidlc-sensor-<id>.ts`)

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Role of Linear | Projection / human board, not the pull source |
| Role of AI-DLC | Canon for units, sequence, sensors, audit, construction gate |
| Issue birth | When the unit is created (not at claim, not only at Dev Complete) |
| Pre-claim content | **B** — edit on Linear; adapter writes title, context, AC back into the unit |
| Post-claim content | Unit → Linear only. Description freeze |
| Status | Always engine → Linear. Human status drags are ignored on the next push |
| Adapter | Deterministic script + `aidlc-journeys` skill (same pattern as `aidlc-agent-verify-sensor.ps1`). Not a Cursor “remember to sync” skill. Not Linear webhooks. Not a new bundled sensor id (harness requires `aidlc-sensor-<id>.ts`) |
| Construction gate | Explicit pull-back: construction blocked until Linear title/description/AC have been copied into unit files |
| Granularity | One Linear issue per AI-DLC unit (name from `unit-of-work-dependency.md` `units[].name`) |
| Binding | Intent record stores `linear-map.yaml` with Linear identifier per unit |
| Commit link | GitHub integration + magic word (`Fixes JOU-123`) preferred; GraphQL `attachmentCreate` fallback |
| Dev Complete | Engine event name. Linear status is **In Review** (BishopLabs Journeys team; no custom Dev Complete status). Not merge → Done |
| Merge → Done | Optional GitHub automation after you merge |
| Transport | Linear GraphQL `https://api.linear.app/graphql` with `LINEAR_API_KEY`. MCP is optional for humans, not the hook kernel |
| Secrets | Environment variable. Never commit the key |
| Commit of this spec | Human only |

---

## 3. Lifecycle

```
Units generated (AI-DLC stage units-generation)
  → adapter upserts one Linear issue per unit (Todo)
  → human reviews/edits cards (title, context, AC)
  → construction gate: pull-back Linear → unit files (required)
  → human approves AI-DLC construction gate
  → unit claimed (`aidlc engine` / `aidlc-unit claim`)
  → freeze description; Linear status → In Progress
  → construction comments (progress only)
  → gates pass + commit exists
  → Linear status → In Review (engine: Dev Complete); commit linked
  → human opens/merges PR (Done via you / optional GitHub on merge)
```

Headless “pull next Linear ticket” remains out of scope. Construction is still human-kicked in AI-DLC.

---

## 4. Sync contract

### Unit → Linear (always, after issue exists)

| Field | When |
|-------|------|
| Create issue (title, description with context + AC) | Unit created |
| Status Todo / In Progress / In Review / Done | Matching engine events (`complete` → In Review) |
| Comments: files touched, tests, blockers | Construction progress |
| Commit URL / SHA attachment | Dev Complete if GitHub did not already attach |

### Linear → unit (before claim only)

| Field | Written into |
|-------|----------------|
| Title | Unit title overlay in `unit-of-work.md` plus `linear-map.yaml` `title` |
| Description body (business context + exit criteria / AC) | Unit description overlay; construction must read the pulled text |
| Optional: Linear comments | Unit diary as review notes (not AC) |

### Never Linear → unit

Status, assignee, cycle, estimate, priority, project, parent. These do not drive the engine.

### Freeze

On unit claim (engine event; Linear shows In Progress):

- Pull-back of title/description/AC stops.
- Linear description edits are ignored, or overwritten on the next unit → Linear push.
- Comments may still be posted by the adapter.

---

## 5. Linear workflow (team settings)

Statuses are per-team. BishopLabs **Journeys** uses Linear defaults. Do not add a custom Dev Complete status. Map engine `complete` to **In Review**.

| Category | Status | Who moves it | Trigger |
|----------|--------|--------------|---------|
| Unstarted | Todo | Adapter | Unit created |
| Started | In Progress | Adapter | Unit claimed |
| Started | In Review | Adapter (`complete`) | Gates pass and commit exists |
| Completed | Done | You / optional GitHub automation | PR merged to protected default |

Leave Backlog and Duplicate unused. Canceled maps to engine `cancel`.

Configure GitHub pull-request automations so **merge → Done** only. Do not let GitHub or Linear move an issue to In Review on PR open (that status is reserved for engine complete). Do not let “on git branch copy” or “on open in coding tool” auto-move to Started — those fight engine-owned status and look like Linear Agent as implementer.

Do not enable Linear Agent coding sessions on this workspace for Journeys implementation.

---

## 6. Commit binding

Preferred:

1. After the issue exists, branch name includes the Linear id.
2. Commit and/or PR uses a closing magic word: `Fixes JOU-123` (team key from config).
3. Linear GitHub app + commit-linking webhook attaches the commit/PR to the issue.

Fallback (local commit not yet seen by GitHub, or integration not wired):

- Adapter posts a comment with the full SHA.
- Adapter creates a URL attachment to the GitHub commit page. SHA is not a first-class Linear field.

GitHub Enterprise Server: commit linking is not supported; use PR linking and/or the attachment fallback.

You still own `git commit` / `git push` / PR merge unless you explicitly ask an agent to commit in that message.

Until merge→Done policy is confirmed for the team, prefer non-closing magic words (`ref JOU-123`) on in-progress commits; use `Fixes` when the commit is meant to close after merge.

---

## 7. Adapter (script + skill)

Same binding pattern as `scripts/aidlc-agent-verify-sensor.ps1` + `.agents/skills/aidlc-journeys/SKILL.md`. The conductor runs named `-Action` verbs; it does not invent GraphQL.

| Hook moment | Action |
|-------------|--------|
| Units generated / stories written | `upsert` — preview create count; `issueCreate` only after `-ApproveCreate N` matches and `N <= maxCreate`; write `linear-map.yaml` |
| Construction gate (before first claim) | `pull-back` — fail closed if Linear unreachable, config missing, or a unit has no issue id |
| Unit claimed | `claim` — In Progress; set `frozen: true` |
| Construction progress | `comment` — throttled; not every file save |
| Unit complete + commit SHA known | `complete` — Dev Complete + commit link |
| Intent parked / cancelled | `cancel` |

Config (committed example, local file gitignored): team id, `maxCreate` (default 25), status names (Todo, In Progress, In Review as `devComplete`, Done), optional GitHub repo URL for commit attachments.

Map file: `<record>/linear-map.yaml` (intent record dir).

---

## 8. Error handling

| Failure | Response |
|---------|----------|
| Linear unreachable at unit create | Fail the hook; do not pretend the board exists |
| Linear unreachable at pull-back | Construction gate stays closed |
| Unit missing Linear id at pull-back | Fail closed; upsert then retry |
| Human edited Linear after freeze | Ignore or overwrite from unit; do not merge silently into AC |
| Human moved Linear status | Next adapter push restores engine status |
| GitHub did not attach commit | Fallback comment + URL attachment; still set Dev Complete |
| Linear Agent starts a coding session on a Journeys issue | Out of policy; disable / ignore |
| Adapter would commit unit-file pull-back | Stage the unit files; **you** commit unless you asked for a commit in that message |
| `LINEAR_API_KEY` unset | Fail closed with a one-line message pointing at `docs/developer/linear-aidlc-projection.md` |
| Create count over `maxCreate` | Fail closed; do not `issueCreate` |
| `-ApproveCreate` missing or not equal to pending create count | Fail closed with the unit list; wait for human confirmation |

---

## 9. Components

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| AI-DLC units | Canon stories + AC | Agents claim these | Intent record (`unit-of-work.md`) |
| Linear issues | Human board | Review before run; watch after | Linear team + statuses |
| `linear-aidlc-projection.ps1` | Upsert, pull-back, push, comments | Skill-invoked verbs | Linear GraphQL, map file |
| GitHub integration | Commit/PR attachments | Magic words | Linear GitHub app |
| Human | Edit stories pre-claim; merge | Always | — |

Changing Linear’s UI must not require rewriting `docs/product/`. Changing AI-DLC version must not require a new ticket taxonomy — only skill/script entry points.

This adapter does **not** invent a product capability id.

---

## 10. Testing / verification

1. Generate units in a dry intent → N Linear issues in Todo, identifiers stored in `linear-map.yaml`.
2. Edit AC on Linear → pull-back updates unit overlay; construction skill refuses to proceed without a successful pull-back.
3. Claim a unit → Linear In Progress; further Linear AC edits do not change the unit file.
4. Simulated construction comments appear on the issue.
5. With a commit SHA, issue is Dev Complete and the commit is visible on the issue.
6. Dragging an issue to Done by hand does not complete the unit; next push restores status.
7. `agent-verify` still fail-closed; this adapter does not weaken it.
8. `aidlc doctor` still passes.

No Journeys.Core / API product unit-test changes. Script tests mock GraphQL (no live Linear required for CI).

---

## 11. Implementation order

See `docs/plans/2026-09-14-Journeys-linear-aidlc-projection.md`.

1. Linear workspace/team + statuses + GitHub integration (human, outside this repo).
2. Developer doc, config example, map schema.
3. `upsert` / `pull-back` / `claim` / `comment` / `complete`.
4. Bind `aidlc-journeys` skill (fail closed at construction).
5. Script tests + `agent-verify` waiver for graph (no product meaning change).

---

## 12. Open at install (not design forks)

These are environment facts, not architecture choices:

- Linear workspace and team key (identifier prefix)
- Team GraphQL `id` and workflow state ids (or names the script resolves once)
- Whether GitHub commit-linking webhook is enabled on `DerekJeremias-personal/Journeys`
- `LINEAR_API_KEY` in the operator environment

---

## 13. Relationship to v1 OS spec

| 2026-09-13 statement | This spec |
|----------------------|-----------|
| Linear is a v1 non-goal | Still true for Phase 0/1. This is Phase 2 |
| Stories / tickets = AI-DLC units | Unchanged. Linear is a projection of those units |
| No ticket poller | Unchanged |
| Human merge | Unchanged |
