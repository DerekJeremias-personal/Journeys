# Functional Design Questions — `u1-tree-hydrate`

Ingested from approved spec `docs/specs/2026-09-18-Journeys-mvp-engine-closeout-design.md`, plan, US1.1 / AC1.1.1–AC1.1.3, C1 appendix. No `Journeys.UX`. Execute-from-spec (2026-09-19).

## Q1. Collect set

A. Recursively collect earn RuleSets and NavConstraint trees (including nested composites and Children); keep existing TTL / decay / RuleState / upsert (recommended)
B. Root `campaign.Journey.Rules` only
X. Other (please specify)

[Answer]: A. Recursively collect earn RuleSets and NavConstraint trees (including nested composites and Children); keep existing TTL / decay / RuleState / upsert (2026-09-19, **Mode:** Chat)

## Q2. Boundaries

A. `RulesService.HydrateState` / `JourneyNode` collect only — no `EventService` or ledger change (recommended)
B. Also change ProcessEvent order
X. Other (please specify)

[Answer]: A. `RulesService.HydrateState` / `JourneyNode` collect only — no `EventService` or ledger change (2026-09-19, **Mode:** Chat)

## Q3. Errors and tests

A. Empty `Rules` does not throw; TDD owns the collect seam; existing root `RuleServiceTests` stay green as non-regression (recommended)
B. Empty `Rules` fails the event
X. Other (please specify)

[Answer]: A. Empty `Rules` does not throw; TDD owns the collect seam; existing root `RuleServiceTests` stay green as non-regression (2026-09-19, **Mode:** Chat)

## Consolidated Summary Confirmation

Does this all look correct before I generate the artifact?

- Looks correct
- Request changes

[Answer]: Looks correct
