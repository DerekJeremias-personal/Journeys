# Entities — `u3-expire-on-process`

ProcessEvent order under the existing lock. Confirmed Looks correct 2026-09-20. Consumes C2 hop clock; does not own dest math. No new account type store, lock table, sweep, or process route.

```yaml
entities:
  - name: ProcessEvent
    description: Existing tenant process turn. C1 HTTP path unchanged. U3 owns only post-lock order.
    attributes:
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: modelName, type: string, required: true, unique: false }
      - { name: loyaltyAccountId, type: string, required: false, unique: false, references: LoyaltyAccount }
    constraints:
      - no new /points/expire, /v2, or process verb
    relationships:
      - { to: LoyaltyAccount, cardinality: "N:1", direction: event-to-account }

  - name: AccountLock
    description: Existing same-account ProcessEvent lease. Reused; not a new mutex or queue.
    attributes:
      - { name: loyaltyAccountId, type: string, required: true, unique: false, references: LoyaltyAccount }
      - { name: acquired, type: boolean, required: true, unique: false }
    constraints:
      - TryLockAccount lease + retries only (NFR5)
    relationships:
      - { to: LoyaltyAccount, cardinality: "N:1", direction: lock-to-account }

  - name: LoyaltyAccount
    description: Owner of ledgers. Bring-current assigns PointLedgers after a successful lock.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: pointLedgers, type: LedgerEntry[], required: false, unique: false, references: LedgerEntry }
      - { name: valid, type: boolean, required: true, unique: false }
    constraints:
      - invalid, missing, or pre-save wrapper accounts skip bring-current
    relationships:
      - { to: LedgerEntry, cardinality: "1:N", direction: account-to-ledgers }

  - name: LedgerEntry
    description: Points row that may hop when due. Dest clock is C2 (U2); U3 does not invent a second formula.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: earnDate, type: datetime, required: false, unique: false }
      - { name: expirationDate, type: datetime, required: false, unique: false }
      - { name: pointAccountTypeId, type: string, required: true, unique: false }
    constraints:
      - due means expirationDate <= now on bring-current
    relationships:
      - { to: LoyaltyAccount, cardinality: "N:1", direction: entry-to-account }
```

## Summary

U3 reshapes ProcessEvent **order**, not ledger schema. After a valid account is populated, `AccountLock` is acquired, then due `LedgerEntry` rows are brought current (C2 hops), then navigation / rules see the released hold.
