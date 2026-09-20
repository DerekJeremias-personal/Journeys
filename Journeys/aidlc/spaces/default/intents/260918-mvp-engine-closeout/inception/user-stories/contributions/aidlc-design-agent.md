**Collaborator:** aidlc-design-agent

## Contribution

This increment has no `Journeys.UX`. The operator experience is one ProcessEvent telling the truth: later-event journey state, earn+365 expiry, this-turn released holds, and a closed webhook POST. Do not add screens, consoles, or a consumer / end-customer persona.

**Personas.** Keep the two role slugs. `program-operator` is the primary actor: high tech comfort via existing API/MCP, configures occasionally, cares on every member event. Goals and pain points match the four engine gaps as lived program outcomes, not UI friction. `technical-buyer` is secondary (US5.1 only). MCP `NotificationConfigId` stays AC on US4.1 (AC4.1.6), not a second authoring journey. The relationship line is right: buyer honesty is not a second UI.

**Stories as the interaction surface.** US1.1–US4.1 are correctly ProcessEvent-observable Given/When/Then. US2.1’s earn-date example is the operator’s clock (Escrow 30 → Spendable 365 → Expired). US4.1’s closed payload is the third-party interface: tenant/account/campaign/rule/outcome/event ids and `awardedAtUtc` only — no event body, ledgers, or secrets. Error and empty paths already exist as event outcomes (skip invalid accounts; missing dest PAT stays put; missing/inactive webhook config → null; `false` send → `IsAwarded` false; throw fails the event unless the host switch is on). Do not invent toasts, retries UI, or a settings screen for NFR6.

**Won’t Have.** UX screens, hosted sweep, GET/reconcile outer lock, email/Twilio adapters, and new capability ids must stay out. US5.1 is meaning-docs honesty for the buyer, not a documentation UI.

**Fold if the lead agrees.** (1) Rewrite US3.1’s so-that in operator language: this event can spend the released hold — drop `PointBalanceProvider` from the benefit clause (keep it in AC if the seam must be named). (2) On AC1.1.2 and AC1.1.3, keep When as ProcessEvent (hydrate / tree walk are how, not who) so the actor stays `program-operator`.

## Positions

- AGREE: No consumer / end-customer persona and no invented screens — value is ProcessEvent-observable, matching Q2/Q3 and the UX-out constraint.
- AGREE: `program-operator` owns Must stories US1.1–US4.1; `technical-buyer` owns only US5.1; MCP proof stays AC4.1.6.
- AGREE: Error edges stay AC on the Must seams (fail-closed event, non-fatal send-false, missing dest PAT) rather than recovery screens.
- AGREE: US4.1 closed webhook payload is the third-party experience; NFR6 switch is host config, not tenant UX.
- OBJECT: US3.1 so-that names `PointBalanceProvider` — operator benefit is “this event can spend the released hold,” not a provider name.
- OBJECT: AC1.1.2 and AC1.1.3 When clauses name HydrateState / tree walk — keep When as ProcessEvent so the actor stays program-operator.
