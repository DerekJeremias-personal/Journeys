# Security test instructions

## Stance

Team-affirmed: do not invent in-tree SAST, DAST, secret, or dependency scanners this increment. Construction verify is not a security scanner. Auth0 tenant `"hayward"` stays human-gated. Ledger/money-like outcomes stay human-gated (NFR4).

NFR Design was skipped. Security checks that *are* in increment are already encoded as closeout tests and onion rules.

## Checks that run with the unit oracles

| NFR | Check | How |
|-----|-------|-----|
| NFR1 Tenant isolation | Webhook send uses the event tenant and that tenant’s `NotificationConfig` only | `NotificationOutcomeTests` + Core `TenantId` on the process path |
| NFR2 Secrets / payload hygiene | Closed `NotificationOutcomePayload` field list; do not log secrets, full payloads, webhook auth headers, or raw audit JSON | `NotificationOutcomeTests` payload shape; review does not add log statements |
| NFR5 Concurrency | Same-account ProcessEvent still serializes on `TryLockAccount`; expire runs under that lease | `ProcessCampaignsExpireOnProcess` |
| NFR6 Webhook-throw switch | Default fail-closed; optional `Journeys:NotificationOutcome:TreatSendThrowAsFalse` | `NotificationOutcomeTests` |

## What is not run here

- SAST/DAST/secret scan jobs (absent by affirmation)
- Auth0 configuration changes
- Live webhook POST to a third party

## How to run

Use the same filtered commands as `integration-test-instructions.md`. There is no separate security runner.
