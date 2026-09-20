# Requirements Analysis Questions

Intent `mvp-engine-closeout`. Ingested approved spec `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md` (Status Approved). Do not reopen G1–G4, capability ids, UX, hosted sweep, Award signature, or webhook payload field list. Practices Q1–Q6 already set Methodology `custom`, no coverage floor, no CD, no scanners.

## Sources

- Approved spec: `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`
- Approved plan: `docs/plans/2026-09-18-Journeys-mvp-engine-closeout.md`
- CodeKB: `business-overview.md`, `architecture.md`, `code-structure.md`
- Affirmed practices: `team.md` / `project.md` (2026-09-19)

## Q1. Already-moved ledger clocks

Some Spendable rows were already cascaded with the old clock (`UtcNow` at the move, not earn + dest days). This increment changes the formula for **new** hops. What should happen to those existing rows?

A. Leave them as-is until they hop again — only new cascade writes use earn + dest days
B. On the next ProcessEvent / bring-current, rewrite dest `ExpirationDate` to `EarnDate + dest days` for already-moved rows
C. Something else we already do — describe it
X. Other (please specify)

[Answer]: A. Leave them as-is until they hop again — only new cascade writes use earn + dest days (2026-09-19, **Mode:** Guide me)

## Q2. Bring-current fails during ProcessEvent

If expiring due ledgers throws after the account is loaded and before campaigns run, the event either stops or continues with stale balances. Which should the requirement be?

A. Fail the whole ProcessEvent — do not navigate or award this turn
B. Log and continue campaigns with the ledgers as loaded (stale holds may still apply)
X. Other (please specify)

[Answer]: A. Fail the whole ProcessEvent — do not navigate or award this turn (2026-09-19, **Mode:** Guide me)

## Q3. Missing destination point account type on cascade

If a due entry’s `ExpiresToPointAccountTypeId` is missing or not loadable for this tenant, what should the hop do?

A. Leave the entry in the source PAT; log; do not fail the whole event
B. Fail the whole ProcessEvent
C. Drop / archive the entry without a dest PAT
X. Other (please specify)

[Answer]: A. Leave the entry in the source PAT; log; do not fail the whole event (2026-09-19, **Mode:** Guide me)

## Q4. Webhook adapter throws

The spec already says a `false` send is non-fatal (`IsAwarded = false`, siblings stay). If `RestApiAdapter` / `SendNotificationAsync` throws (timeout, network), same rule or fail the event?

A. Catch, log, `IsAwarded = false`, do not fail ProcessEvent (same as `false`)
B. Let the throw fail ProcessEvent
X. Other (please specify)

[Answer]: B. Let the throw fail ProcessEvent by default; configurable to A (catch, log, IsAwarded = false, do not fail ProcessEvent) (2026-09-19, **Mode:** Guide me; user: "2, but make it configurable to turn on 1")

## Q5. Concurrent ProcessEvent on the same account

Two events can hit the same loyalty account close together. This increment does not add a new lock. Confirm the requirement.

A. No new locking — keep today’s wrapper / account write behavior; at-least-once webhooks stay as specified
B. This increment must add an explicit same-account lock or queue
X. Other (please specify)

[Answer]: A. No new locking — keep today’s TryLockAccount; ProcessEvent bring-current runs inside that existing lock (after acquire, before rules). At-least-once webhooks stay as specified (2026-09-19, **Mode:** Guide me; user: bring current within the existing lock)

## Q6. GET / reconcile bring-current locking

`GetLoyaltyAccount*` and reconcile can call bring-current with no outer `TryLockAccount`. `ExpirePoints` still locks when it finds due rows; it does not lock when nothing is due. The “assumes already locked” comment is about the caller. Should this increment change those GET/reconcile paths?

A. Not this increment — ProcessEvent only (bring-current under the existing campaign lock). GET/reconcile stay as they are
B. This increment also take `TryLockAccount` before bring-current on GET/reconcile
X. Other (please specify)

[Answer]: A. Not this increment — ProcessEvent only (bring-current under the existing campaign lock). GET/reconcile stay as they are (2026-09-19, **Mode:** Guide me)

## Consolidated Summary Confirmation

Does this all look correct before I generate the requirements artifact?

- Q1: Leave already-moved Spendable clocks as-is; only new cascade writes use earn + dest days
- Q2: If bring-current throws, fail the whole ProcessEvent (no navigate/award)
- Q3: Missing dest PAT leaves the entry in the source PAT, logs, does not fail the event
- Q4: Webhook throw fails ProcessEvent by default; a config switch can treat throws as non-fatal (`IsAwarded = false`, siblings stay). Assumption: existing host/appsettings (or equivalent), no new capability id
- Q5: No new lock; ProcessEvent bring-current runs inside existing `TryLockAccount` (lock → expire due rows → rules)
- Q6: GET/reconcile bring-current locking is not this increment
- Ingested spec still owns G1–G4, payload shape, earn-date formula, tree hydrate, non-goals (no UX, no hosted sweep, no Award signature change)

- Looks correct
- Request changes

[Answer]: Looks correct
