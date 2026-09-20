# Entities — `u2-earn-date-cascade`

Ledger hop clock. Confirmed Looks correct 2026-09-19. Human-gated money-like math. No new account type store.

```yaml
entities:
  - name: LedgerEntry
    description: A points row that may hop via ExpiresToPointAccountTypeId when due.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: earnDate, type: datetime, required: false, unique: false, constraints: "unchanged on hop" }
      - { name: expirationDate, type: datetime, required: false, unique: false }
      - { name: pointAccountTypeId, type: string, required: true, unique: false, references: PointAccountType }
    constraints:
      - hop keeps earnDate
    relationships:
      - { to: PointAccountType, cardinality: "N:1", direction: entry-to-source-or-dest }

  - name: PointAccountType
    description: Tenant PAT that supplies dest clock (end-date, else days, else none).
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: pointsLifespanDays, type: integer, required: false, unique: false }
      - { name: pointsLifespanEndDate, type: datetime, required: false, unique: false }
      - { name: expiresToPointAccountTypeId, type: string, required: false, unique: false, references: PointAccountType }
    constraints:
      - tenant isolation — dest PAT must load for this tenant
    relationships:
      - { to: PointAccountType, cardinality: "0..1", direction: expires-to }

  - name: LoyaltyAccount
    description: Owner of ledgers. Not reshaped. Bring-current (U3) consumes this hop.
    attributes:
      - { name: id, type: string, required: true, unique: true }
      - { name: tenantId, type: string, required: true, unique: false }
      - { name: pointLedgers, type: LedgerEntry[], required: false, unique: false, references: LedgerEntry }
    constraints: []
    relationships:
      - { to: LedgerEntry, cardinality: "1:N", direction: account-to-ledgers }
```

## Summary

Hop input is `LedgerEntry` plus dest `PointAccountType`. `EarnDate` is identity of the earn instant; dest expiration is computed, never `UtcNow + days` on the move.
