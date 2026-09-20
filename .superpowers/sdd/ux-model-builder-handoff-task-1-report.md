# Task 1 report: Schema metadata parse + brief builder

**Status:** DONE  
**Commits:** none

## TDD evidence

### RED (tests first, implementation absent)

Command:

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-brief.test.ts src/services/loyalty/parse-list.test.ts
```

Result: FAIL (expected)

- `src/lib/model-builder-brief.test.ts` — `Cannot find module './model-builder-brief'`
- `normalizeSchema > reads tag and ModelMetaData keys` — `expected undefined to deeply equal { Wrapper: 'w1', NaturalKeySymbols: '["orderid"]' }`
- Existing PascalCase `normalizeSchema` case still passed after adding `tag: undefined` / `modelMetaData: undefined` to the expect (missing vs `undefined` treated as equal by Vitest `toEqual`)
- 2 files failed; 1 failed + 16 passed of 17 collected tests (brief suite did not collect)

### GREEN (exact brief implementation)

Same focused command.

Result: PASS

```
✓ src/lib/model-builder-brief.test.ts (4 tests)
✓ src/services/loyalty/parse-list.test.ts (17 tests)
Test Files  2 passed (2)
     Tests  21 passed (21)
```

### Full suite (once)

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Result: PASS — 48 files, 235 tests.

## Files changed

| Path | Action |
|------|--------|
| `Journeys.UX/src/lib/api-types.ts` | Modified — `SchemaListItem.modelMetaData?: Record<string, string>` (`tag` already present) |
| `Journeys.UX/src/services/loyalty/parse-list.ts` | Modified — `readModelMetaData`; `normalizeSchema` sets `modelMetaData` |
| `Journeys.UX/src/services/loyalty/parse-list.test.ts` | Modified — PascalCase expect + ModelMetaData test |
| `Journeys.UX/src/lib/model-builder-brief.ts` | Created |
| `Journeys.UX/src/lib/model-builder-brief.test.ts` | Created |

No Model save APIs. No Backend.Model.UX. No capability ids invented. `modelId "unknown"` is never used. Brief body is not logged.
