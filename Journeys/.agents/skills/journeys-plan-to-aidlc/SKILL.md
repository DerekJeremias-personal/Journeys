---
name: journeys-plan-to-aidlc
description: >
  Journeys handoff from Superpowers planning to AI-DLC execution. Use when
  writing or editing docs/plans/, finishing /brainstorming, the user approves
  a spec/plan, or they say implement / go / execute / start construction.
---

# Plan with Superpowers; execute with `/aidlc classic`

This repo splits planning and construction. Superpowers (`/brainstorming`, writing-plans) owns the **spec** (`docs/specs/`) and **plan** (`docs/plans/` — not `docs/superpowers/plans/`). AWS AI-DLC owns **execution** from Initialization onward (`classic` already skips AI-DLC Ideation). Do not implement production code from Superpowers SDD.

Generic Superpowers `writing-plans` requires `subagent-driven-development`. **That line is void here.** `AGENTS.md` and `aidlc/spaces/default/memory/project.md` win.

## When writing `docs/plans/*.md`

Start the plan with this header (not the Superpowers SDD blurb):

```markdown
> **Execution:** After approval, say execute and the intent name.
> `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not**
> use superpowers:subagent-driven-development. Linear unit issues land before
> any `Journeys.*` code. Do not `--review none` or Express.
```

Keep task checkboxes, file map, tests, and constraints. Those become input to AI-DLC units-generation, not SDD tickets and not Linear issues.

After the human approves the spec and plan, do **not** start SDD. Tell them they can say `execute` plus the intent name. Wait for that unless they already gave it.

## When the user says execute / go / implement

This chat may continue. Do **not** print a paste block and stop. Do **not** open product code. Become the AI-DLC conductor after the preflight below.

### Preflight (ask, then stop the turn, if anything is uncertain)

1. **Spec.** Resolve one file under `docs/specs/`. Prefer the plan's `**Spec:**` line, else this thread. Status must be **Approved**, or the human must have approved it in this chat. If missing or not approved: ask, do not start Classic.
2. **Plan.** Matching file under `docs/plans/`. If several candidates: numbered question, wait.
3. **Intent.** Required, from the human. If they said execute with no intent: ask for a short name, wait. Do not invent one.
4. **Active workflow.** Run `aidlc engine orchestrate next --status`. If another intent is in progress, follow `.cursor/skills/aidlc/SKILL.md` new-work routing (ask resume vs this work vs `--new-intent`). Do not silently replace it. If `--new-intent` is confirmed, the engine may require a fresh chat — honor that print.
5. Refuse Superpowers SDD / executing-plans. Refuse Express and `--review none`.

### Start Classic

Read `.cursor/skills/aidlc/SKILL.md` and run its forwarding loop. First engine call (PowerShell), description in single quotes:

```powershell
aidlc engine orchestrate next --scope classic -- 'Brownfield Journeys. Approved spec: docs/specs/<file>.md. Approved plan: docs/plans/<file>.md. Intent: <intent>. Ingest those files; do not re-brainstorm. Linear unit issues before any Journeys.* code.'
```

Act on `directive.kind` exactly as that skill says. Also keep `.agents/skills/aidlc-journeys/SKILL.md` loaded for canon, verify, and Linear.

### Ingest (do not re-elicit the design)

When a stage loads context, treat the approved spec path as the requirements source and the approved plan as the decomposition input. Trace them into AI-DLC artifacts. Do not run a second brainstorm. Do not copy plan checkboxes onto Linear.

- **2.3 requirements-analysis:** the spec is the authoritative product request (one explicit path).
- **2.4 user-stories** (if it runs): derive from spec + plan.
- **2.6 domain-design** (if it runs): existing onion; do not greenfield.
- **2.7 units-generation:** plan file map / tasks inform unit boundaries. Units remain AI-DLC artifacts (`unit-of-work.md`). Those units are the Linear stories.

`/aidlc compose` may drop AWS/CDK/operation later if the human asks. Do not compose away units-generation or delivery-planning.

### Linear hard gate (before any `Journeys.*` write)

After **units-generation** is fully complete (reviewer + human gate + `report --result completed`), do **not** call `next` until upsert has succeeded.

Resolve `<record>` as the directory under `aidlc/spaces/default/intents/` that contains `aidlc-state.md`.

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>"
```

If that exits non-zero with `would create N Linear issues`: show the unit list, wait for the human to confirm **N**, then:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>" -ApproveCreate N
```

Do not invent N. `maxCreate` is a ceiling. Then tell the human the stories are in Linear **Todo** and wait until they say the board is ready (they may edit titles/AC first).

**Construction still stays closed** until pull-back succeeds:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
```

Non-zero: halt. Do not claim a unit. Do not start **code-generation**. Do not edit `Journeys.*`, `Journeys.UX`, or `Journeys.Tests` except as an already-claimed unit after this gate. Delivery-planning (2.9) may run after upsert; it must not write product code.

If `linear-map.yaml` is missing an `issueId` for any unit, fail closed. Then follow `aidlc-journeys` for claim / comment / complete.

## When `/aidlc` is already running

Follow `.agents/skills/aidlc-journeys/SKILL.md`. This skill does not replace the conductor.
