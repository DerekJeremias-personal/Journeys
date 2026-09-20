# Rules — `u2-earn-date-cascade`

Confirmed Looks correct 2026-09-19.

```yaml
rules:
  - id: BR2.1
    statement: Cascade hop keeps EarnDate.
    category: calculation
    applies_to: LedgerEntry.earnDate
    trigger: SaveLedgerExpirations hop
    logic: IF a due entry hops to dest THEN EarnDate is unchanged.
    violation: Dest clock starts at move time.
    source: FR2.1, AC2.1.1

  - id: BR2.2
    statement: Dest PointsLifespanEndDate wins over days.
    category: calculation
    applies_to: LedgerEntry.expirationDate
    trigger: Hop when dest end-date is set
    logic: IF dest.pointsLifespanEndDate is set THEN dest ExpirationDate is that instant.
    violation: Days overwrite a configured end-date.
    source: FR2.4, AC2.1.4

  - id: BR2.3
    statement: Else dest days apply from EarnDate.
    category: calculation
    applies_to: LedgerEntry.expirationDate
    trigger: Hop when dest has days and no end-date
    logic: IF dest.pointsLifespanDays is set AND EarnDate is present THEN dest ExpirationDate is EarnDate plus those days.
    violation: UtcNow + days on the move.
    source: FR2.1, AC2.1.1

  - id: BR2.4
    statement: Neither dest end-date nor dest days leaves dest expiration unset.
    category: calculation
    applies_to: LedgerEntry.expirationDate
    trigger: Hop when dest has neither clock
    logic: IF dest has neither PointsLifespanEndDate nor PointsLifespanDays THEN dest ExpirationDate is unset.
    violation: Invented 100-day default.
    source: FR2.3, AC2.1.5

  - id: BR2.5
    statement: Missing EarnDate may use existing ExpirationDate only when dest days exist; never UtcNow as lifespan base.
    category: calculation
    applies_to: LedgerEntry.expirationDate
    trigger: Hop when dest has days and EarnDate is missing
    logic: IF dest days are set AND EarnDate is missing THEN use existing ExpirationDate as last resort; do not use UtcNow as the lifespan base.
    violation: Move-time clock.
    source: FR2.2, C2

  - id: BR2.6
    statement: Missing or not-found dest PAT leaves the row in source and does not fail the event.
    category: policy
    applies_to: LedgerEntry
    trigger: Hop when dest id cannot load for the tenant
    logic: IF dest PAT id is missing or not found for the tenant THEN keep the row in the source PAT, log, continue ProcessEvent.
    violation: Dropped row or failed event on a missing dest.
    source: FR2.5, AC2.1.3

  - id: BR2.7
    statement: Unexpected dest-PAT infrastructure throw fails ProcessEvent.
    category: policy
    applies_to: ProcessEvent
    trigger: Infra throw while loading dest PAT
    logic: IF load or persist throws unexpectedly THEN fail the event; do not navigate or award that turn.
    violation: Partial hop then rules.
    source: FR2.6, AC2.1.6

  - id: BR2.8
    statement: Already-cascaded UtcNow dest dates stay until they hop again.
    category: policy
    applies_to: LedgerEntry.expirationDate
    trigger: Later process without a new hop
    logic: IF a row was already written with the old UtcNow clock AND it has not hopped again THEN leave ExpirationDate as written.
    violation: Silent rewrite of historical dest dates.
    source: FR2.4, AC2.1.2
```

## Summary

| ID | Category | Trigger |
|----|----------|---------|
| BR2.1 | calculation | Keep EarnDate |
| BR2.2 | calculation | End-date wins |
| BR2.3 | calculation | Earn + dest days |
| BR2.4 | calculation | Neither → unset |
| BR2.5 | calculation | Last-resort existing expiration |
| BR2.6 | policy | Missing dest PAT |
| BR2.7 | policy | Infra throw |
| BR2.8 | policy | Old UtcNow rows stay |
