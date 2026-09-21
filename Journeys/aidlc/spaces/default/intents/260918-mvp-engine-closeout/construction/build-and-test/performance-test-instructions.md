# Performance test instructions

## Applicability

NFR Requirements and NFR Design were skipped for this workflow. Inception NFRs (NFR1–NFR7) are tenant isolation, secrets hygiene, at-least-once send, ledger human-gate, existing lock concurrency, webhook-throw switch, and test posture. None of them set latency, throughput, or load budgets.

This increment adds no load, soak, or benchmark suite. Do not invent k6/JMeter jobs or a production-like environment to satisfy a target that was never written.

## What is not run here

| Check | Reason |
|-------|--------|
| Load / p95 latency | No performance NFR |
| Soak / capacity | No performance NFR; Operation `performance-validation` is skipped in this scope |
| Auto-scale | No AWS/CDK work this increment |

## If a later intent adds a number

Record the target ID, expected value, and owning stage. Until then there is no executable performance command for Build and Test.
