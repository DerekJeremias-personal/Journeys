<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
- 2026-09-19T19:58:00Z — Mix heuristic: B1 hydrate, B2 cascade, B3 expire+webhook. Three serial Bolts; no WSJF scores. Bolts are delivery slices; Construction stays stage-major unless later switched.

## Deviations
- 2026-09-19T19:58:00Z — Delayed independent u4 until B3 so EventService is edited once. Allowed by the DAG; recorded as economic hold, not a missing edge.

## Tradeoffs
- 2026-09-19T19:58:00Z — Bundled U3+U4 instead of four Bolts or webhook-first. Shared lock/bring-current and NotificationService host-copy outweigh starting the webhook in parallel with hydrate.

## Open questions
- 2026-09-19T19:58:00Z — Host/appsettings key name for non-fatal webhook throw remains a Construction name (carried from Contract Design).

