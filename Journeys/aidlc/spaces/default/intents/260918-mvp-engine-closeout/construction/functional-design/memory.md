<!-- INVARIANT: examples are single-line HTML comments so a fresh template parses to total=0 (MEMORY_EMPTY). Do NOT un-comment or split across lines. t100 guards this. -->
> This file is kept up to date automatically while the stage runs. Add observations at the review step, not by editing here directly.

## Interpretations
- 2026-09-19T20:15:00Z — Wave batch 0 ingested the approved spec for u1, u2, and u4. No frontend-components (library units). Host copies the notification port onto engine state, matching Units Generation not the older spec sentence that named RulesService.


- 2026-09-19T20:15:00Z — Collect walk is the only new behaviour; existing hydrate pipeline stays.
<!-- aidlc-wave-memory:u1-tree-hydrate:d6edfee75341bd624fb17b4416e61c539c5d584b1782dc0db91efd2dbd2da6b5 -->


- 2026-09-19T20:15:00Z — Dest clock matches C2 including last-resort existing ExpirationDate under dest days only.
<!-- aidlc-wave-memory:u2-earn-date-cascade:3f08df776c25e3bcec0eabe43c82f59e8ef141af5f138e7af006c1237b259542 -->


- 2026-09-19T20:15:00Z — Throw fails ProcessEvent by default; host switch is the non-fatal path. Port on engine state.
<!-- aidlc-wave-memory:u4-notification-webhook:077bb21150b394ec0e4fd6d5243027412c81ba9f60b0181c88445f1b6af55319 -->


- 2026-09-20T11:40:00Z — Lock then bring-current then rules; consume C2; skip invalid/missing/pre-save; no new lock or /points/expire.
<!-- aidlc-wave-memory:u3-expire-on-process:050f8f66bdf5f3340d2da1d50b26962573f52163143ab728227c2d891f67f108 -->

## Deviations
- 2026-09-19T20:15:00Z — Webhook throw is fail-the-event by default (Domain/Contract), not the spec's original non-fatal Award. Host switch remains.

## Tradeoffs
- 2026-09-19T20:15:00Z — One confirmation turn covered three question files; only u1 received the engine receipt on that turn.

## Open questions
- 2026-09-19T20:15:00Z — Host/appsettings key name for non-fatal webhook throw still Construction.


- 2026-09-19T20:15:00Z — Exact host key name for the throw switch is still open.
<!-- aidlc-wave-memory:u4-notification-webhook:ee391aa19f91688c8a9e55e84ee4a17b986b18cef93a94887abcf98d169f720b -->
