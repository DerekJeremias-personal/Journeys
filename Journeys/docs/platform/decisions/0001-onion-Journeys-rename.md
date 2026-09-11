# ADR 0001: Onion layout and Elevate.ELP → Journeys rename

**Status:** Accepted  
**Date:** 2026-09-08

## Context

The program runtime was copied from an `Elevate.ELP.*` solution into `C:\Dev\Journeys` with the same onion shape.

## Decision

- Keep onion project boundaries (DTO, Core, DAL, Infra, API, Agent, Notification, Tests).
- Code identity token: `Elevate.ELP` → `Journeys` (drop `ELP`, PascalCase).
- `Backend.*` assembly names and HintPaths stay unchanged.
- Agent/docs contract lives next to `Journeys.sln`, not at `C:\Dev\Journeys\` root.

## Consequences

Agents load `AGENTS.md` from the solution folder. Tools under `..\tools\` are documented, not a second root.
