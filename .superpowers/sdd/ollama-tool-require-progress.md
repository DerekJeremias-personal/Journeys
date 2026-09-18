# SDD progress — Ollama tool require

Plan: `Journeys/docs/plans/2026-09-17-Journeys-campaign-agent-ollama-tool-require.md`
Branch: `feat/journeys-ux-loyalty-shell`
(Do not confuse with `ollama-llm-progress.md`.)

- [x] Task 1 OllamaToolRequire — 9/9 pass; review Approved. No commit.
- [x] Task 2 Pre-brief SaveModel — 14/14 pass; review Approved. No commit. Minor: class summary still says non-mutating backend tools.
- [ ] Task 3 Orchestrator wire + docs + verify — code+docs done; scoped tests 35/35; `agent-verify: OK` without Journeys.Tests in `-Files`. Task review **Needs fixes** (Important, plan-mandated): brief lists Journeys.Tests files in `agent-verify -Files`, which runs the entire suite; 44 pre-existing `BatchJobAdapterIntegrationTests` fail against live Backend. Human must choose: accept scoped gate, or halt until those 44 are green. No commit.

Minors for whole-branch review:
- Task 2: `CampaignWorkflowToolFilter` class summary still says non-mutating backend tools.
- Task 3 first review: no orchestrator-level test that mapped prompt gets `RequireAny` (second review dropped this).

Stopped Journeys.API pid 14540 so default sln build could copy DLLs. Restart API before G3/G4 live smoke.

No commit unless the user asks.
