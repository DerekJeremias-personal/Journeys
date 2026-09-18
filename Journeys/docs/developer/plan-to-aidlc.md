# Plan to AI-DLC

Planning is Superpowers. Execution is `/aidlc classic`, started by `.agents/skills/journeys-plan-to-aidlc/SKILL.md` after you approve the spec/plan and give an intent name.

## Why this exists

Generic Superpowers `writing-plans` injects a **REQUIRED SUB-SKILL** for `subagent-driven-development`. That header hijacks execution. Journeys voids it.

`classic` already skips AI-DLC Ideation. The Superpowers spec + plan **is** ideation and planning. Handoff is immediately after that, into Initialization — not into Construction.

## Ritual

1. `/brainstorming` → spec under `docs/specs/`. You approve.
2. writing-plans → plan under `docs/plans/` with the **Execution** header from the skill. You approve.
3. You say `execute` and the intent name (same chat is fine).
4. The skill preflights (approved spec, matching plan, intent, no colliding workflow), then runs `aidlc engine orchestrate next --scope classic` and follows `.cursor/skills/aidlc/SKILL.md`.
5. Inception **ingests** the spec/plan (does not re-brainstorm). **units-generation** mints the stories.
6. Linear `upsert` → you confirm `-ApproveCreate N` → you edit cards if needed → `pull-back`.
7. Only then: construction claims units and writes `Journeys.*`. `aidlc-journeys` binds verify and Linear claim/complete.

If another AI-DLC intent is already active, the skill asks; `--new-intent` may require a fresh chat (engine rule).

Do not use Express or `--review none` on production work.
