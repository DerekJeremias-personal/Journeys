# Task 7 Report: Docs, graph, onion overlay

**Task:** Docs, graph, onion overlay (loyalty-shell plan, task 7)  
**Working directory:** `C:\Dev\Journeys\Journeys`  
**Date:** 2026-09-14

## Status

**DONE_WITH_CONCERNS** — every brief file was created or modified with the exact bodies/YAML fragments. Step 10 `Select-String` could not run (Shell blocked). Capability ids were verified by reading `docs/product/graph/nodes.yaml`. No `Journeys.UX` product code was changed. No git commit.

## Commits

None (per task instructions).

## Files created

| File | Role |
|------|------|
| `docs/developer/journeys-ux.md` | Exact body from brief Step 1 |

## Files modified

| File | Change |
|------|--------|
| `docs/developer/index.md` | Added `- [Journeys.UX](journeys-ux.md) — Loyalty admin Next app` |
| `docs/developer/local-ops.md` | Appended `## Journeys.UX` pointing at `journeys-ux.md` |
| `docs/platform/overlays.md` | Added UI row `{Product}.UX` → `Journeys.UX` (HTTP to API only; not a csproj) |
| `docs/platform/architecture.md` | UI row = `Journeys.UX`; Must not = project-reference Core/Adapters and own writes; UX named as HTTP client after the C# arrow; “Not in this solution” no longer claims Next UI is missing |
| `docs/roadmap/non-goals.md` | Deleted UI/`ui-in-this-sln` bullet; kept “Copying EXP monorepo files” |
| `docs/product/graph/nodes.yaml` | Added `proj-ux`; deleted `ui-in-this-sln` |
| `docs/product/graph/edges.yaml` | Deleted `ui-in-this-sln` `NOT_YET` `campaigns`; appended `proj-ux` `CONSTRAINED_BY` `tenant-id-required` |
| `docs/product/graph/path-map.yaml` | Prefixed `Journeys.UX` → `[campaigns]`, `meaningOptional: false` |
| `scripts/path-docs-map.yaml` | Prefixed `Journeys.UX` docs: journeys-ux.md, architecture.md, overlays.md |
| `AGENTS.md` | First paragraph: onion sln + sibling Next.js HTTP client, not a C# project |
| `.cursor/rules/onion-architecture.mdc` | Replaced “UI is not in this solution…” with HTTP-only UX + no Core/DAL/Infra refs + no frontend inside C# onion |

**Not edited (per brief):** `docs/platform/coding-standards.md`, any `Journeys.UX` product code.

## Architecture extras (plan + parent instructions)

- Layers table UI **This product:** `` `Journeys.UX` ``
- Layers table UI **Must not:** `Project-reference Core or Adapters. Own writes of campaigns/accounts.`
- Center→edge C# arrow unchanged: `` `Journeys.DTO` -> `Journeys.Core` -> `Journeys.DAL` / `Journeys.Infra*` / `Journeys.Notification` -> `Journeys.API` / `Journeys.Agent` ``
- Same sentence then: `` `Journeys.UX` is an HTTP client of `Journeys.API` (not in this C# arrow). ``
- Kept: “A UI never becomes the write authority.”
- `` **Not in this solution:** terraform, Databricks. `` (Next UI no longer listed as absent)

## Graph rules (spec §10)

| Rule | Result |
|------|--------|
| Add `proj-ux` (Project, name Journeys.UX) | Yes |
| Delete `ui-in-this-sln` from `nodes.yaml` | Yes — gone |
| Delete `ui-in-this-sln` `NOT_YET` `campaigns` | Yes — gone |
| Add `proj-ux` `CONSTRAINED_BY` `tenant-id-required` | Yes |
| Do **not** add `campaigns` `IMPLEMENTED_AS` `proj-ux` | Confirmed; `campaigns` still `IMPLEMENTED_AS` `proj-core` only |
| No new capability ids | Confirmed; seven capabilities unchanged |
| path-map `Journeys.UX` → `[campaigns]` `meaningOptional: false` | Yes, top of `entries:` |
| path-docs-map `Journeys.UX` docs triple | Yes, top of `entries:` |

## Step 10 result (capability ids)

Shell `Select-String` **did not run** (preToolUse hook blocked the terminal). Equivalent check by reading `docs/product/graph/nodes.yaml`:

**Capability ids (unchanged seven):**

- `event-models`
- `campaigns`
- `journeys`
- `rules-engine`
- `outcomes`
- `campaign-agent`
- `mcp-api`

**Added project:** `proj-ux`  
**Removed:** `ui-in-this-sln` (absent from `nodes.yaml` and `edges.yaml`)

All `id:` lines in `nodes.yaml`: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`, `proj-dto`, `proj-core`, `proj-dal`, `proj-infra`, `proj-api`, `proj-agent`, `proj-notification`, `proj-tests`, `proj-ux`, `technical-buyer`, `program-operator`, `campaign-author`, `tenant-admin`, `author-campaign`, `process-event`, `tenant-id-required`, `no-controller-business-logic`, `backend-dll-external`, `terraform-in-this-tree`.

## Self-review

| Check | Result |
|-------|--------|
| `journeys-ux.md` exact brief body | Yes |
| index + local-ops pointers | Yes |
| overlays UI row exact | Yes |
| architecture UI row + HTTP client + write-authority sentence | Yes |
| non-goals: UI bullet gone; EXP copy kept | Yes |
| `ui-in-this-sln` gone from graph YAML | Yes |
| no `campaigns` `IMPLEMENTED_AS` `proj-ux` | Yes |
| seven capabilities only | Yes |
| AGENTS.md first paragraph | Yes |
| onion UI bullet | Yes |
| coding-standards.md untouched | Yes |
| Journeys.UX product code untouched | Yes |
| no git commit | Yes |
| Step 10 Select-String | Not run (shell blocked); Read-verified |

## Concerns

1. **Step 10 command not executed.** Shell was blocked. Capability-id confirmation is from a full read of `nodes.yaml`, not `Select-String`. Re-run locally if a command receipt is required:

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern 'id:' | ForEach-Object { $_.Line }
```

2. **Out-of-scope leftover.** `docs/product/taxonomies/constraints.md` still has row `no-ui-in-sln` (“React/Next UI is out of this solution”). That file was **not** in this task’s list. Graph node id was `ui-in-this-sln` (now deleted); the taxonomy id never matched. Task 8 or a follow-up should retire that row so product docs do not contradict the graph.

3. **Impact scripts not run here.** Task 8 owns `docs-impact` / `graph-impact`. This change set includes the required product graph + developer/platform docs, so those scripts should pass when Task 8 runs them.

## Recommended controller check

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern 'id:' | ForEach-Object { $_.Line }
```

Expected: existing seven capabilities plus `proj-ux`; **no** `ui-in-this-sln`.
