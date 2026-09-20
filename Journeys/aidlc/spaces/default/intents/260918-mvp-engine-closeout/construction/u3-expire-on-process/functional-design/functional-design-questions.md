# Functional Design Questions — `u3-expire-on-process`

Ingested from approved spec (placement superseded by Units/Contract), US3.1 / AC3.1.1–AC3.1.3, C1 order, C2 consume. Execute-from-spec (2026-09-20).

## Q1. ProcessEvent order

A. After a valid account is populated: existing `TryLockAccount`, then bring-current (C2 hops), then navigate / rules. No new lock, sweep, or `/points/expire` (recommended)
B. Bring-current before the lock, outside ProcessCampaigns
C. New hosted sweep this increment
X. Other (please specify)

[Answer]: A. After a valid account is populated: existing `TryLockAccount`, then bring-current (C2 hops), then navigate / rules. No new lock, sweep, or `/points/expire` (2026-09-20, **Mode:** Chat)

## Q2. Skip and failure

A. Invalid / missing / pre-save wrapper accounts skip bring-current. Throw after the lock fails the event. GET/reconcile callers unchanged (recommended)
B. Always bring-current, including missing accounts
X. Other (please specify)

[Answer]: A. Invalid / missing / pre-save wrapper accounts skip bring-current. Throw after the lock fails the event. GET/reconcile callers unchanged (2026-09-20, **Mode:** Chat)

## Q3. Clock ownership

A. Consume U2 dest clock only — do not invent a second hop formula (recommended)
B. Re-implement dest expiration in EventService
X. Other (please specify)

[Answer]: A. Consume U2 dest clock only — do not invent a second hop formula (2026-09-20, **Mode:** Chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
