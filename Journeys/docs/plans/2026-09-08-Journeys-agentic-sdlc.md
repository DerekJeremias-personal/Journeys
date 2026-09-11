# Journeys Agentic SDLC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Place the semantic and execution control planes under `C:\Dev\Journeys\Journeys\` so agents load product meaning and path policy, then prove `docs-impact`, `graph-impact`, and `agent-verify` work.

**Architecture:** Article layout (`docs/product|roadmap|platform|developer`, `docs/specs`, `docs/plans`) with Cursor-native `AGENTS.md`, `.cursor/rules`, `.agents/skills`. Graph is Neo4j-ready YAML. Scripts are PowerShell; no EXP copies.

**Tech Stack:** Markdown, YAML, PowerShell 5+, `dotnet build` on net8.0 `Journeys.sln`.

## Global Constraints

- Do not copy files from EXP.
- Do not modify `c:\Dev\loyalty\ELP`.
- Do not git commit, push, or init unless the user asks.
- Do not stand up Neo4j or add a Bolt client.
- Rename token and Backend DLLs are out of scope (already done).
- Human plan-approval rules from the old `.cursorrules` stay in `AGENTS.md`.

---

### Task 1: Door + path rules

**Files:** Create `AGENTS.md`, `README.md`; replace `.cursorrules` with a pointer; create `.cursor/rules/*.mdc` as listed in the spec.

- [ ] Write `AGENTS.md` with the spec reading order, DoD, human-only authorities, tools pointer.
- [ ] Write `README.md` pointing at `AGENTS.md`.
- [ ] Replace `.cursorrules` with a pointer to `AGENTS.md` and `.cursor/rules`.
- [ ] Write always-on and glob rules from the old `.cursorrules` body (API/Core/DAL/Infra/Tests/Agent).
- [ ] Do not commit.

---

### Task 2: Semantic + platform + roadmap + developer seed

**Files:** All `docs/product/**`, `docs/roadmap/**`, `docs/platform/**`, `docs/developer/**` listed in the spec (spec file already exists).

- [ ] Write taxonomies, ontology, use-cases, market-and-positioning.
- [ ] Write `nodes.yaml`, `edges.yaml`, `path-map.yaml`, `neo4j-adapter.md`, `waivers/.gitkeep`.
- [ ] Write platform architecture + ADR 0001, roadmap, developer local-ops/testing/tools.
- [ ] Do not commit.

---

### Task 3: Scripts and skills

**Files:** `scripts/*.ps1`, `scripts/path-docs-map.yaml`, `.agents/skills/*/SKILL.md`.

- [ ] Implement changed-file discovery (`-Files` or git diff).
- [ ] `docs-impact.ps1` and `graph-impact.ps1` with longest-prefix match; `meaningOptional` for Infra.
- [ ] `agent-verify.ps1` runs both then `dotnet build Journeys.sln`.
- [ ] Skills document exact commands.
- [ ] Do not commit.

---

### Task 4: Verify per spec section 9

- [ ] Confirm tree; `.cursorrules` is a pointer.
- [ ] Simulate Core change without docs/graph → impact scripts exit 1.
- [ ] Same change with docs + graph update or waiver → exit 0.
- [ ] `agent-verify.ps1` builds the solution (use waiver or include required docs in `-Files`).
- [ ] Do not commit.
