# SDD progress — Campaign Agent Ollama

Plan: `Journeys/docs/plans/2026-09-16-Journeys-campaign-agent-ollama.md`
Branch: `feat/journeys-ux-loyalty-shell`
(Do not confuse with `ux-loyalty-shell-progress.md`.)

- [x] Task 1 CampaignAgentLlmProvider — tests 12/12 pass; review Approved.
- [x] Task 2 Journeys.Infra.Llm options/handler/applier — tests 24/24; Logging.Abstractions 8.0.3 (NU1605); review Approved.
- [x] Task 3 factory + connect retry — 10/10; retry inside FI; IsUnreachable tightened; review Approved after fix.
- [x] Task 4 DI branch AddCampaignAgentLlm — isolated API build 0 CS; review Approved.
- [x] Task 5 Ollama addendum + Dev diet — CampaignAgent 340/340; content-root metadata + JSON comments; review Approved.
- [x] Task 6 docs/graph/overlays — campaign-agent-llm.md, path-map, overlays; review Approved.
- [x] Task 7 verify — docs-impact OK, graph-impact OK, sln build 0 CS errors (isolated -o). CampaignAgent tests 340/340. Full suite 49 fails under `-o TEMP` (UserService/ExpirePoints/Chunking/Examples/BatchJob — not this change). Human G6 smoke still required.

No commit.
