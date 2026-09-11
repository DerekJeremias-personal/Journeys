# Design: Journeys agentic SDLC information architecture

**Date:** 2026-09-08  
**Status:** Approved for implementation  
**Scope:** Place agent-loadable semantic and execution control planes under `C:\Dev\Journeys\Journeys\`, seed product meaning (Neo4j-ready files), fold `.cursorrules` into `AGENTS.md` + path rules, and ship working `docs-impact` / `graph-impact` / `agent-verify` scripts.  
**Placement authority:** [Part 1](https://www.linkedin.com/pulse/fully-agentic-sdlc-information-architecture-problem-derek-jeremias-hae3c/) and [Part 2](https://www.linkedin.com/pulse/fully-agentic-sdlc-information-architecture-problem-derek-jeremias-4tgmc/). EXP is an example of *kinds* of files only — do not copy EXP content, Nx, Aerospike, or BFF rules.  
**If articles and EXP disagree:** follow the articles.

---

## 1. Problem and goals

Journeys is a .NET onion solution with a single `.cursorrules` file. Agents cannot find a reading order, product meaning, path policy, or a code-change ↔ docs/meaning bridge. Without that, they optimize for “compiles” and invent labels.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Door** | `AGENTS.md` at the solution folder is the session contract; `README.md` points at the same contract |
| G2 | **Semantic plane** | `docs/product/` has taxonomies, ontology, use-cases, market positioning, and a graph with stable node ids |
| G3 | **Neo4j-ready** | Graph files use labels/edges a future Bolt adapter would load; no Neo4j required now |
| G4 | **Execution plane** | Path-scoped `.cursor/rules`, Journeys skills, `docs/platform|roadmap|developer`, `docs/specs` + `docs/plans` |
| G5 | **Bridge** | Working `scripts/docs-impact` and `scripts/graph-impact` plus `scripts/agent-verify` |
| G6 | **One contract** | Existing `.cursorrules` body moves into AGENTS.md + rules; leftover `.cursorrules` is a pointer only |

### Non-goals

- Copying or moving files from EXP
- Git hooks, CI, ADO/GitHub PR gates
- Standing up Neo4j or writing a live Bolt client
- Changing onion architecture, renaming assemblies, or product behavior
- Including UX/UI, terraform, or Databricks in this tree
- Auto-merge or agents owning merge/release

---

## 2. Decisions (brainstorming)

| Topic | Choice |
|-------|--------|
| Depth | **B** — seed layout + working impact/verify scripts; hooks/CI later |
| Root | **B** — all of this under `C:\Dev\Journeys\Journeys\` (next to `Journeys.sln`) |
| `.cursorrules` | **A** — fold into `AGENTS.md` + `.cursor/rules/*.mdc`; pointer left behind |
| Product meaning | **A** — real seed (not stubs, not full GTM dump) |
| Layout style | **1** — Cursor-native names (`AGENTS.md`, `.cursor/rules`, `.agents/skills`) matching article *kinds* |
| Spec/plan folders | **A** — `docs/specs/` and `docs/plans/` (not `docs/superpowers/`) |
| Graph storage | **A** — files first, Neo4j-ready ids/labels/edges; markdown/YAML is canon until Neo4j exists |

Human rules from today’s `.cursorrules` stay binding in `AGENTS.md`: plan for non-trivial code changes; do not implement without expressed approval of the plan; do not delete existing comments without cause.

---

## 3. Target tree

```
C:\Dev\Journeys\Journeys\
  AGENTS.md
  README.md
  Journeys.sln
  .cursorrules                          # pointer only
  .cursor/rules/
    session-context.mdc                 # alwaysApply: reading order
    docs-and-graph-sync.mdc             # alwaysApply: source canonical; same-changeset updates
    onion-architecture.mdc              # alwaysApply: DTO/Core/DAL/Infra/API
    api-controllers.mdc                 # glob Journeys.API/**
    core-services.mdc                   # glob Journeys.Core/**
    dal-adapters.mdc                    # glob Journeys.DAL/**
    infra-adapters.mdc                  # glob Journeys.Infra*/** and Journeys.Infra/**
    tests.mdc                           # glob Journeys.Tests/**
    campaign-agent.mdc                  # glob CampaignAgent, Mcp, A2a, Journeys.Agent/**
  .agents/skills/
    Journeys-workspace/SKILL.md
    docs-impact/SKILL.md
    graph-impact/SKILL.md
    agent-verify/SKILL.md
  docs/
    product/
      index.md
      taxonomies/
        capabilities.md
        domains.md
        personas.md
        lifecycle.md
        constraints.md
      ontology/
        campaign.md
        journey.md
        event-model.md
        rule.md
        tenant.md
        draft-live.md
      graph/
        nodes.yaml
        edges.yaml
        path-map.yaml                   # code prefix → node ids
        waivers/                        # explicit no-impact waivers
        neo4j-adapter.md                # contract for future Bolt; no runtime client
      use-cases/
        author-campaign.md
        process-event.md
      market-and-positioning.md
    roadmap/
      index.md
      end-state.md
      non-goals.md
    platform/
      index.md
      architecture.md
      decisions/
        0001-onion-Journeys-rename.md
    developer/
      index.md
      testing.md
      local-ops.md
      tools.md                          # pointer to ..\tools\
    specs/                              # this file lives here
    plans/
  scripts/
    docs-impact.ps1
    graph-impact.ps1
    agent-verify.ps1
    path-docs-map.yaml
```

`C:\Dev\Journeys\tools\` is documented from `docs/developer/tools.md` and `AGENTS.md`. It does not get a second contract tree.

---

## 4. Session contract (`AGENTS.md`)

**Reading order (every session, before explore/edit):**

1. This file (`AGENTS.md`)
2. `docs/product/index.md` plus the taxonomy/ontology/graph slice for the task
3. `docs/roadmap/` if the work is a feature (why now, non-goals)
4. `docs/platform/index.md` and architecture
5. Path rules that match files you will touch
6. Spec + plan under `docs/specs` and `docs/plans` for any multi-file or non-trivial change

**Hierarchy when instructions conflict:** current-turn user instruction → `AGENTS.md` + `.cursor/rules` → `.agents/skills` → generic plugin skills → default model behavior.

**Definition of done (agent):** code green for the blast radius + required tests + `docs-impact` and `graph-impact` clear (update or waiver) + ready for human merge. Not “I added files.”

**Human-only:** intent/prioritization; approve non-trivial design; merge/release; risk surfaces (auth, money-like ledger outcomes, tenant isolation). Agents never merge.

**Pointer:** `..\tools\` (CampaignContextAudit) is outside this folder; load `docs/developer/tools.md` when touching it.

---

## 5. Semantic plane

### Taxonomies (controlled labels)

- **Capabilities:** `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`
- **Domains (implemented as projects):** `Journeys.DTO`, `Journeys.Core`, `Journeys.DAL`, `Journeys.Infra.*`, `Journeys.API`, `Journeys.Agent`, `Journeys.Notification`, `Journeys.Tests`
- **Personas:** `program-operator`, `technical-buyer`, `campaign-author`, `tenant-admin`
- **Lifecycle:** `draft`, `live`, `extend`, `invent`
- **Constraints:** `tenant-id-required`, `no-controller-business-logic`, `backend-dll-external`

### Ontology (meaning here)

- **Campaign:** executable program (not a config row); Draft/Live; same APIs for agent and engine
- **Journey:** stateful graph of program logic
- **Event model:** customer-owned schema; Backend data plane holds instances; symbols feed rules
- **Rule:** declarative, evaluated by the polymorphic engine in Core
- **Tenant:** all business activity is tenant-scoped
- **Draft vs Live:** mutation policy is draft-first where the product already requires it

### Graph files (Neo4j-ready)

`nodes.yaml` records: `id`, `label` (`Capability`, `Domain`, `Persona`, `UseCase`, `Project`, `Constraint`, `NonGoal`), `name`, `props`.

`edges.yaml` records: `from`, `type`, `to`. Allowed types: `NEEDS`, `REALIZED_BY`, `IMPLEMENTED_AS`, `SERVES`, `DEPENDS_ON`, `EXTENDS`, `CONSTRAINED_BY`, `NOT_YET`.

Example edges (seed, not exhaustive):

- `campaign-author` `NEEDS` `author-campaign`
- `author-campaign` `REALIZED_BY` `campaign-agent`
- `campaign-agent` `IMPLEMENTED_AS` `Journeys.API` (CampaignAgent/MCP) and `Journeys.Agent`
- `rules-engine` `IMPLEMENTED_AS` `Journeys.Core`
- `campaigns` `DEPENDS_ON` `event-models`
- `ui-in-this-sln` `NOT_YET` (UI is out of this solution)

**Canon until Neo4j exists:** these YAML files. `docs/product/graph/neo4j-adapter.md` specifies: Bolt URL from env, map labels 1:1, merge on `id`, never a second silent graph. `graph-impact` reads YAML only in this spec.

### Path → node map

`docs/product/graph/path-map.yaml` maps glob prefixes under the solution folder to node `id`s. Examples:

| Path prefix | Node ids |
|-------------|----------|
| `Journeys.API/CampaignAgent/**` | `campaign-agent`, `campaigns` |
| `Journeys.API/Mcp/**` | `mcp-api` |
| `Journeys.Core/RulesEngine/**` | `rules-engine`, `journeys` |
| `Journeys.Core/**` | `campaigns` (default Core) |
| `Journeys.DTO/**` | `campaigns` |
| `Journeys.DAL/**` | `campaigns` |
| `Journeys.Infra*/**` | (infra constraint nodes; waive if no product-meaning change) |
| `Journeys.Tests/**` | inherit from the production area under test |
| `Journeys.Agent/**` | `campaign-agent` |

---

## 6. Execution plane

### Path rules (content sourced from current `.cursorrules`, split by glob)

- **API:** no business logic in controllers; DTOs in/out; prefer services over adapters; MCP/A2A/CampaignAgent call the same services
- **Core:** services + interfaces; journeys/rules live here; no Azure SDKs
- **DAL:** persistence; `APIErrorsException` for field validation
- **Infra:** Cosmos, blob, auth, service bus, Backend adapters only
- **Tests:** `Journeys.Tests`, existing layout, AAA
- **Always-on onion:** TenantId on business operations; do not strip comments without cause

### Skills

Each skill is a short procedure: when to run, exact script, expected exit code. No Nx/Kong/Turbo content.

### Specs and plans

Non-trivial work uses `docs/specs/YYYY-MM-DD-<topic>-design.md` and `docs/plans/YYYY-MM-DD-<topic>.md`. Specs name graph node ids touched and whether the change **extends** or **invents**.

### Platform / developer / roadmap seed

- `docs/platform/architecture.md` — onion, HintPath `..\..\Binaries\Backend.*.dll`, project list
- ADR `0001-onion-Journeys-rename.md` — rename token `Elevate.ELP` → `Journeys`; Backend names unchanged
- `docs/developer/local-ops.md` — `dotnet restore` / `build` on `Journeys.sln`
- `docs/roadmap/end-state.md` — program runtime: events → journeys/rules → outcomes
- `docs/roadmap/non-goals.md` — UI not in this sln; terraform/Databricks not in this tree; no copy from EXP

---

## 7. Scripts

PowerShell, run from `C:\Dev\Journeys\Journeys`.

**Changed-file discovery:** if `.git` exists, use `git diff --name-only` (staged + unstaged vs HEAD). Else require `-Files` or scan a named list; do not fail closed with “not a git repo” without documenting the `-Files` usage in the skill.

**`docs-impact.ps1`:** load `scripts/path-docs-map.yaml`; for each changed file matching a prefix, required docs must be in the same change set (or listed as already updated). Exit 1 if missing.

**`graph-impact.ps1`:** load `path-map.yaml`; required node ids must appear in a diff under `docs/product/` **or** a waiver file under `docs/product/graph/waivers/` with reason. Exit 1 if neither.

**`agent-verify.ps1`:** run both impact scripts, then `dotnet build Journeys.sln`. Optionally `dotnet test` on `Journeys.Tests` when that project is in the changed set. Exit 0 only if all invoked steps succeed.

Do not copy `obj` from loyalty. Do not modify `c:\Dev\loyalty\ELP`.

---

## 8. Error handling

| Failure | Response |
|---------|----------|
| Agent skips reading order | `session-context.mdc` alwaysApply; AGENTS.md is explicit |
| Docs/code disagree | Fix docs/meaning in the same change; source wins |
| Impact script false positive on Infra-only change | Waiver with reason, or path-map marks prefix as meaning-optional |
| No git | `-Files` parameter; skill documents it |
| Neo4j not running | Expected; YAML remains canon |

---

## 9. Verification (this spec’s implementation)

1. Tree matches Section 3.
2. `.cursorrules` is a pointer; onion rules exist under `.cursor/rules`.
3. `nodes.yaml` / `edges.yaml` / `path-map.yaml` parse as YAML and use the labels/edge types in Section 5.
4. `docs-impact.ps1` and `graph-impact.ps1` exit 1 on a simulated Core change with no docs/graph update, exit 0 when updates or a waiver are present.
5. `agent-verify.ps1` builds `Journeys.sln` successfully on a clean tree after a waiver or matched updates.
6. EXP files are not copied into Journeys.

---

## 10. Out of scope (later)

- Pre-commit / pre-push hooks and CI
- Neo4j container + Bolt loader
- Independent review-pass automation
- Playwright / UI e2e (no UI in this sln)
- Making `tools\` a second AGENTS.md root
