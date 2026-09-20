### Task 1: Schema metadata parse + brief builder

**Files:**
- Modify: `Journeys.UX/src/lib/api-types.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.test.ts`
- Create: `Journeys.UX/src/lib/model-builder-brief.ts`
- Create: `Journeys.UX/src/lib/model-builder-brief.test.ts`

**Interfaces:**
- Consumes: `SchemaListItem`, `normalizeSchema`, `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME`
- Produces:
  - `REQUIRED_EVENT_MODEL_METADATA_KEYS = ["Wrapper", "NaturalKeySymbols", "AccountXIdSymbol", "TimeOfOccurrence"] as const`
  - `readModelMetaData(row: Record<string, unknown>): Record<string, string>`
  - `normalizeSchema` also sets `modelMetaData`
  - `includeInModelBuilderCatalog(schema: SchemaListItem): boolean`
  - `missingEventModelMetadata(schema: SchemaListItem): string[]`
  - `buildModelBuilderBrief(args: { tenantId: string; schemas: SchemaListItem[]; catalogUnavailable?: boolean }): string`

- [ ] **Step 1: Failing parse + brief tests**

Add to `parse-list.test.ts` inside `describe("normalizeSchema")`:

```ts
  it("reads tag and ModelMetaData keys", () => {
    const s = normalizeSchema({
      Id: "guid-1",
      Name: "OrderPlaced",
      Tag: "eventable",
      ModelMetaData: { Wrapper: "w1", NaturalKeySymbols: "[\"orderid\"]" }
    });
    expect(s?.tag).toBe("eventable");
    expect(s?.modelMetaData).toEqual({
      Wrapper: "w1",
      NaturalKeySymbols: "[\"orderid\"]"
    });
  });
```

Update the existing PascalCase expect to include `tag: undefined` and `modelMetaData: undefined` if the object equality would otherwise fail.

Create `Journeys.UX/src/lib/model-builder-brief.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import {
  REQUIRED_EVENT_MODEL_METADATA_KEYS,
  buildModelBuilderBrief,
  includeInModelBuilderCatalog,
  missingEventModelMetadata
} from "./model-builder-brief";
import type { SchemaListItem } from "./api-types";

const order: SchemaListItem = {
  id: "guid-1",
  name: "OrderPlaced",
  status: "Live",
  modelType: "loyalty",
  tag: "eventable",
  modelMetaData: { NaturalKeySymbols: "[\"orderid\"]" }
};

describe("model-builder-brief", () => {
  it("lists the four required metadata names in the contract", () => {
    const brief = buildModelBuilderBrief({ tenantId: "acme", schemas: [] });
    for (const key of REQUIRED_EVENT_MODEL_METADATA_KEYS) {
      expect(brief).toContain(key);
    }
    expect(brief).toContain('Never use modelId "unknown".');
  });

  it("includes eventable and LoyaltyAccountDetails; omits other tags", () => {
    expect(includeInModelBuilderCatalog(order)).toBe(true);
    expect(
      includeInModelBuilderCatalog({
        name: "LoyaltyAccountDetails",
        modelType: "loyalty"
      })
    ).toBe(true);
    expect(
      includeInModelBuilderCatalog({
        name: "Other",
        modelType: "loyalty",
        tag: "reference"
      })
    ).toBe(false);
  });

  it("flags missing Wrapper and does not embed journey JSON", () => {
    expect(missingEventModelMetadata(order)).toEqual([
      "Wrapper",
      "AccountXIdSymbol",
      "TimeOfOccurrence"
    ]);
    const brief = buildModelBuilderBrief({
      tenantId: "acme",
      schemas: [order, { name: "Other", journey: { id: "nope" } } as SchemaListItem]
    });
    expect(brief).toContain("OrderPlaced");
    expect(brief).toContain("missing: Wrapper");
    expect(brief).not.toContain("\"journey\"");
    expect(brief).not.toContain('id=unknown');
  });

  it("adds catalog warning when GetMany failed", () => {
    expect(
      buildModelBuilderBrief({ tenantId: "acme", schemas: [], catalogUnavailable: true })
    ).toContain("Catalog snapshot unavailable");
  });
});
```

- [ ] **Step 2: Run â€” expect FAIL**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-brief.test.ts src/services/loyalty/parse-list.test.ts
```

Expected: FAIL (missing `modelMetaData` / module).

- [ ] **Step 3: Implement**

`api-types.ts` â€” add to `SchemaListItem`:

```ts
  tag?: string;
  modelMetaData?: Record<string, string>;
```

(`tag` may already exist.)

`parse-list.ts` â€” add:

```ts
export function readModelMetaData(row: Record<string, unknown>): Record<string, string> | undefined {
  const raw = row.modelMetaData ?? row.ModelMetaData;
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) return undefined;
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === "string" && value.trim()) out[key] = value.trim();
  }
  return Object.keys(out).length > 0 ? out : undefined;
}
```

In `normalizeSchema`, set `modelMetaData: readModelMetaData(rec)`.

`model-builder-brief.ts`:

```ts
import type { SchemaListItem } from "@/lib/api-types";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";

export const REQUIRED_EVENT_MODEL_METADATA_KEYS = [
  "Wrapper",
  "NaturalKeySymbols",
  "AccountXIdSymbol",
  "TimeOfOccurrence"
] as const;

export function includeInModelBuilderCatalog(schema: SchemaListItem): boolean {
  const type = schema.modelType?.toLowerCase();
  if (type && type !== "loyalty") return false;
  if (schema.tag?.toLowerCase() === "eventable") return true;
  return schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
}

export function missingEventModelMetadata(schema: SchemaListItem): string[] {
  const meta = schema.modelMetaData ?? {};
  return REQUIRED_EVENT_MODEL_METADATA_KEYS.filter((key) => !meta[key]?.trim());
}

export function buildModelBuilderBrief(args: {
  tenantId: string;
  schemas: SchemaListItem[];
  catalogUnavailable?: boolean;
}): string {
  const lines: string[] = [
    `Opened from Journeys.UX for tenant ${args.tenantId}. Author event-signal (processable) models for this tenant.`,
    "",
    "## Contract",
    "- Payload vs wrapper: customer JSON is WrappedEventPayload.event; the wrapper is the engine envelope.",
    "- Processable models need metadata: Wrapper (wrapper model id), NaturalKeySymbols, AccountXIdSymbol (payload-root path unless loyalty-account-shaped), TimeOfOccurrence.",
    "- Persist wrapper symbols lowercase: naturalkey, timeofoccurrence, lastprocessed, accountid, appliedcampaigns, appliedrulesetids, providerstates, journeystates, outcomestates.",
    "- Campaign.Events lists payload model GUIDs, not display names.",
    "- modelType loyalty. Processable payload models use tag eventable.",
    "- Never use modelId \"unknown\".",
    "",
    "## Catalog snapshot"
  ];
  if (args.catalogUnavailable) {
    lines.push("Catalog snapshot unavailable");
  } else {
    const rows = args.schemas.filter(includeInModelBuilderCatalog);
    if (rows.length === 0) {
      lines.push("(no loyalty eventable models in GetMany)");
    }
    for (const schema of rows) {
      const missing = missingEventModelMetadata(schema);
      const gap = missing.length > 0 ? `missing: ${missing.join(", ")}` : "ok";
      lines.push(
        `- ${schema.name ?? "(unnamed)"} id=${schema.id ?? ""} status=${schema.status ?? ""} tag=${schema.tag ?? ""} ${gap}`
      );
    }
  }
  return lines.join("\n");
}
```

- [ ] **Step 4: Run tests â€” expect PASS**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-brief.test.ts src/services/loyalty/parse-list.test.ts
```

- [ ] **Step 5: Do not commit** unless the user asked.

---

