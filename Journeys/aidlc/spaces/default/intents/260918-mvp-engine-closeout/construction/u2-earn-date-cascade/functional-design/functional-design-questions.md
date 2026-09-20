# Functional Design Questions — `u2-earn-date-cascade`

Ingested from approved spec, US2.1 / AC2.1.1–AC2.1.6, C2 hop schema. Ledger/money-like human-gated. Execute-from-spec (2026-09-19).

## Q1. Dest clock

A. Keep `EarnDate`; dest expiration = dest end-date, else earn + dest days, else unset; last-resort existing `ExpirationDate` only under dest days when earn is missing (recommended)
B. UtcNow + (days ?? 100) on the hop
X. Other (please specify)

[Answer]: A. Keep `EarnDate`; dest expiration = dest end-date, else earn + dest days, else unset; last-resort existing `ExpirationDate` only under dest days when earn is missing (2026-09-19, **Mode:** Chat)

## Q2. Missing dest PAT vs infra throw

A. Missing/not-found dest PAT: stay in source, log, do not fail the event; unexpected dest-PAT infra throw fails ProcessEvent (recommended)
B. Missing dest PAT also fails the event
X. Other (please specify)

[Answer]: A. Missing/not-found dest PAT: stay in source, log, do not fail the event; unexpected dest-PAT infra throw fails ProcessEvent (2026-09-19, **Mode:** Chat)

## Q3. Tests and gate

A. Arrange dest PAT with 365 days (not 30-day Spendable factory); already-cascaded UtcNow rows stay; human reviews clock before merge (recommended)
B. Use Spendable factory days as dest clock
X. Other (please specify)

[Answer]: A. Arrange dest PAT with 365 days (not 30-day Spendable factory); already-cascaded UtcNow rows stay; human reviews clock before merge (2026-09-19, **Mode:** Chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
