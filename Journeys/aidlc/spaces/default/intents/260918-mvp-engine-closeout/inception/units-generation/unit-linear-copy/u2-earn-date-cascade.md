# U2 — `u2-earn-date-cascade`

## U2 — `u2-earn-date-cascade`

**Description:** PAT cascade keeps `EarnDate` and sets dest expiration from dest end-date, else earn + days, else unset.

**Boundaries:** `LoyaltyAccountService.SaveLedgerExpirations` only. Deposit-time EarnDate math stays. No `PointLedgerTypeStrings` redesign.

**Responsibilities:** Earn-date dest clock; missing dest PAT stays in source; dest-PAT infra throw fails ProcessEvent; already-cascaded UtcNow rows stay.

**Constraints:** Human-gated ledger/money-like. Arrange dest PAT with 365 days — not the 30-day Spendable factory.
