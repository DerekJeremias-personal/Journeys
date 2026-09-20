# Rules — `u3-expire-on-process`

Confirmed Looks correct 2026-09-20.

```yaml
rules:
  - id: BR3.1
    statement: After a valid account is populated, acquire the existing TryLockAccount, then bring-current, then navigate / rules.
    category: policy
    applies_to: ProcessEvent
    trigger: ProcessCampaigns after populate
    logic: IF the account is valid and populated THEN lock, then bring-current, then navigation and RuleSet evaluate. Do not run bring-current unlocked before ProcessCampaigns.
    violation: Unlocked expire before ProcessCampaigns, or rules before release.
    source: FR3.4, AC3.1.1

  - id: BR3.2
    statement: Bring-current uses the existing expire of due ledgers and assigns PointLedgers.
    category: calculation
    applies_to: LoyaltyAccount.pointLedgers
    trigger: After lock acquired
    logic: IF lock is held THEN expire rows whose ExpirationDate is due (ExpirationDate <= now) via the existing bring-current, assign loyaltyAccount.PointLedgers, and let this turn's rules see the released hold.
    violation: Rules run against stale ledgers.
    source: FR3.1, AC3.1.1

  - id: BR3.3
    statement: Consume the U2 dest clock only. Do not invent a second hop formula.
    category: constraint
    applies_to: LedgerEntry
    trigger: Hop inside bring-current
    logic: IF a due row hops THEN dest expiration is C2 only (end-date, else EarnDate + dest days, else last-resort existing expiration under dest days, else unset). Keep EarnDate.
    violation: UtcNow + days on the move, or a second clock in EventService.
    source: C2, FR3.1

  - id: BR3.4
    statement: Invalid or missing accounts skip bring-current.
    category: validation
    applies_to: LoyaltyAccount
    trigger: ProcessEvent when account is invalid or not found
    logic: IF the account is invalid or not found THEN skip bring-current and do not invent a sweep.
    violation: Expire against a missing account.
    source: FR3.2, AC3.1.3

  - id: BR3.5
    statement: Pre-save wrapper accounts skip bring-current.
    category: validation
    applies_to: LoyaltyAccount
    trigger: Wrapper path before the account exists
    logic: IF the account is a pre-save wrapper THEN do not call bring-current.
    violation: Expire before the account exists.
    source: FR3.3, AC3.1.3

  - id: BR3.6
    statement: Same-account ProcessEvents serialize on today's TryLockAccount. No new lock, queue, hosted sweep, or /points/expire.
    category: policy
    applies_to: AccountLock
    trigger: Concurrent ProcessEvents for the same account
    logic: IF two ProcessEvents contend THEN they serialize on the existing lease and retries. Do not add a lock, queue, sweep, or expire route.
    violation: New mutex or unlocked concurrent expire.
    source: FR3.4, FR3.2, AC3.1.2, NFR5

  - id: BR3.7
    statement: A throw after the lock fails the whole ProcessEvent.
    category: policy
    applies_to: ProcessEvent
    trigger: Bring-current throw while lock is held
    logic: IF bring-current throws after the lock THEN fail the event. Do not navigate or award that turn.
    violation: Partial expire then rules.
    source: FR3.5, AC3.1.3

  - id: BR3.8
    statement: GET and reconcile bring-current callers stay as they are this increment.
    category: policy
    applies_to: LoyaltyAccount
    trigger: GET or reconcile expire path
    logic: IF the caller is GET or reconcile THEN leave that path unchanged. ExpirePoints still locks when it finds due rows.
    violation: Outer lock rewrite of GET/reconcile this increment.
    source: FR3.6, AC3.1.2
```

## Summary

| ID | Category | Trigger |
|----|----------|---------|
| BR3.1 | policy | Lock then bring-current then rules |
| BR3.2 | calculation | Assign released PointLedgers |
| BR3.3 | constraint | Consume C2 dest clock |
| BR3.4 | validation | Skip invalid / missing |
| BR3.5 | validation | Skip pre-save wrapper |
| BR3.6 | policy | Existing lock only |
| BR3.7 | policy | Throw fails the event |
| BR3.8 | policy | GET / reconcile unchanged |
