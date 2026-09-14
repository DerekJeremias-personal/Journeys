# Task 3 Report: Security, runtime, logging, coding standards

**Status:** DONE  
**Date:** 2026-09-13  
**Commits:** none (per brief)

## Summary

Created four platform/developer documentation pages for security, runtime, logging, and coding standards. Updated both platform and developer index pages to link the new docs per the task brief.

## Files created

### `docs/platform/security.md`

Documents current auth behavior (`RequireLoyaltyAccount`, Auth0, API keys), allow paths, credential handling, human-gated Auth0 debt (`hayward` hardcode), and tenant isolation rules.

### `docs/platform/runtime.md`

Runtime map table covering DAL/Backend persistence, MassTransit/Cosmos sagas, Data Lake/blob, ServiceBus, notifications, auth, and key-value storage.

### `docs/developer/logging.md`

Serilog sink configuration (Azure Analytics vs console), structured `TenantId` logging, and interim redaction rules.

### `docs/developer/coding-standards.md`

Onion layering, DTO conventions, async naming, validation patterns, enum serialization, and test placement extracted from existing rules and habits.

## Files modified

### `docs/platform/index.md`

Replaced with updated index listing Runtime and Security alongside Architecture, Overlays, Decisions, and developer ops.

### `docs/developer/index.md`

Replaced with updated index adding Logging and Coding standards links.

## Verification

```powershell
Select-String -Path docs\platform\security.md -Pattern 'hayward','RequireLoyaltyAccount','Journeys-API-KEY' | Measure-Object | Select-Object -ExpandProperty Count
# Result: 4 (expected ≥ 3)

Select-String -Path docs\platform\runtime.md -Pattern "MassTransit","DataLake","Backend" | Measure-Object | Select-Object -ExpandProperty Count
# Result: 3 (expected ≥ 3)

Select-String -Path docs\platform\index.md -Pattern "security.md","runtime.md" | Measure-Object | Select-Object -ExpandProperty Count
# Result: 2 (expected ≥ 2)
```

All checks passed.

## Scope compliance

- No product C# code changed
- No aidlc config run
- No files modified outside brief scope (except this report)
- No git commit, push, merge, or PR

## Concerns

None.
