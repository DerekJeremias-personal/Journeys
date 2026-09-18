# SDD progress — Journeys.UX unlabeled campaign agent chat

Plan: `Journeys/docs/plans/2026-09-17-Journeys-ux-campaign-agent-chat.md`
Branch: `feat/journeys-ux-loyalty-shell` (work in place; do not commit unless user asks)

- Task 1: complete (no commit; review clean). Minors: no malformed-URL guard; encoding tests brief-limited.
- Task 2: complete (no commit; review clean). Minors: optional CRLF/multi-line data coverage.
- Task 3: complete (no commit; review approved). Minors: AbortError on connect treated as unreachable; 200 with no body silent close; 401 test regex loose; cancel doesn't abort upstream — Task 4 must pass `request.signal`.
- Task 4: complete (no commit; re-review approved). Minors: TransformStream cancel hook may not fire (flush via teardown); empty API base URL throws 500; duration omitted from log.
- Task 5: complete (no commit; review approved). Minors: double-submit race; no role styling; decoder flush; no abort on unmount; textarea flex.
- Task 6: complete (no commit; review approved). Minors: Infra.Llm path-map bundled; journeys-ux screens section still shell-only.
- Task 7: complete (no commit). UX G1/G2/G4/G5 pass. G3 FAIL: API SSE `turn_failed` at `load_history` ("An unexpected error occurred") reproduced with curl against CampaignAgentController. Vitest 63/63.
