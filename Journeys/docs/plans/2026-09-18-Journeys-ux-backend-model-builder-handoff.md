# Journeys.UX Backend Model Builder Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Prefer a fresh subagent per task. Steps use checkbox (`- [ ]`) syntax. Do not start tasks until the user approves this plan.

**Goal:** Enable Loyalty **Model Builder** to open Backend.Model.UX Modeler in a new tab on the same tenant, with a one-time Journeys event-signal brief (fixed contract + GetMany catalog gaps).

**Architecture:** Journeys.UX builds a markdown brief from `event-model.md` rules plus existing `POST api/Model/{tenant}/GetMany`. A form POST (new tab) seeds Backend.Model.UX; that app stores the brief in `sessionStorage` via an HTML handshake and injects it as the first Modeler turn only when `source=journeys`. No Journeys.API writes. No BackEnd.Web calls from Journeys.UX.

**Tech Stack:** Next.js 16, React 19, Vitest in both `Journeys.UX` and `Backend.Model.UX`.

**Spec:** `docs/specs/2026-09-18-Journeys-ux-backend-model-builder-handoff-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- `Journeys.UX` HTTP-only to `Journeys.API`. No Core/DAL/Infra project-reference. No BFF. No BackEnd.Web. No iframe. No `@exp/*`.
- `ModelController` stays GetMany-only. Do not add Model save/patch/delete.
- Do not add `IMPLEMENTED_AS` `proj-ux` on `event-models`.
- Never `modelId` `"unknown"`. Never treat Auth0 `"hayward"` as `TenantId`. Use `resolveTenantId`.
- Do not log the brief body, catalog dump, JWT, API keys, or full campaign JSON. Log tenantId + `model-builder-handoff` only.
- Nav label stays **Model Builder**.
- Do not put the brief in the query string.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Do not name a product tenant in new copy. Tests reuse existing dummy ids (`t`, `guid-1`).

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.UX/src/lib/api-types.ts` | `modelMetaData` on `SchemaListItem` |
| `Journeys.UX/src/services/loyalty/parse-list.ts` | Read `modelMetaData` / `ModelMetaData` |
| `Journeys.UX/src/lib/model-builder-brief.ts` | Pure brief + catalog filter |
| `Journeys.UX/src/lib/model-builder-brief.test.ts` | Contract / missing / no unknown / no journey |
| `Journeys.UX/src/lib/model-builder-handoff.ts` | Base URL + seed action URL helpers |
| `Journeys.UX/src/lib/model-builder-handoff.test.ts` | URL join; empty env |
| `Journeys.UX/src/components/loyalty-nav.tsx` | Link vs disabled |
| `Journeys.UX/src/app/loyalty/models/handoff/page.tsx` | GetMany + form POST |
| `Journeys.UX/.env.example` | `BACKEND_MODEL_UX_BASE_URL` |
| `docs/developer/journeys-ux.md` | Handoff + cache note |
| `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` | NonGoal note |
| `docs/product/graph/waivers/2026-09-18-model-builder-handoff.md` | No new capability |
| `C:\Dev\Backend\Backend.Model.UX\components\layout\tenant-context.tsx` | `?tenantId=` |
| `C:\Dev\Backend\Backend.Model.UX\app\api\model-builder\seed\route.ts` | Form POST → HTML → sessionStorage → redirect |
| `C:\Dev\Backend\Backend.Model.UX\lib\journeys-seed.ts` | sessionStorage key + consume once |
| `C:\Dev\Backend\Backend.Model.UX\components\model-coach\model-coach-client.tsx` | Inject first turn |

---

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

- [ ] **Step 2: Run — expect FAIL**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-brief.test.ts src/services/loyalty/parse-list.test.ts
```

Expected: FAIL (missing `modelMetaData` / module).

- [ ] **Step 3: Implement**

`api-types.ts` — add to `SchemaListItem`:

```ts
  tag?: string;
  modelMetaData?: Record<string, string>;
```

(`tag` may already exist.)

`parse-list.ts` — add:

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

- [ ] **Step 4: Run tests — expect PASS**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-brief.test.ts src/services/loyalty/parse-list.test.ts
```

- [ ] **Step 5: Do not commit** unless the user asked.

---

### Task 2: Nav + handoff page + docs

**Files:**
- Create: `Journeys.UX/src/lib/model-builder-handoff.ts`
- Create: `Journeys.UX/src/lib/model-builder-handoff.test.ts`
- Create: `Journeys.UX/src/app/loyalty/models/handoff/page.tsx`
- Modify: `Journeys.UX/src/components/loyalty-nav.tsx`
- Modify: `Journeys.UX/.env.example`
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (one line: Model Builder nav replaced by handoff spec)
- Create: `docs/product/graph/waivers/2026-09-18-model-builder-handoff.md`

**Interfaces:**
- Consumes: `buildModelBuilderBrief`, `getAllSchemas`, `resolveTenantId`, `auth`
- Produces:
  - `modelBuilderUxBaseUrl(): string` — `process.env.BACKEND_MODEL_UX_BASE_URL?.trim().replace(/\/+$/, "") ?? ""`
  - `modelBuilderSeedActionUrl(baseUrl: string): string` — `${baseUrl}/api/model-builder/seed`
  - Handoff page form `action={seedUrl}` `method="post"` `target="_blank"` fields `tenantId`, `brief`, `source` = `journeys`

- [ ] **Step 1: Failing handoff URL tests**

```ts
import { describe, expect, it, vi } from "vitest";
import { modelBuilderSeedActionUrl, modelBuilderUxBaseUrl } from "./model-builder-handoff";

describe("model-builder-handoff", () => {
  it("returns empty base when unset", () => {
    vi.stubEnv("BACKEND_MODEL_UX_BASE_URL", "");
    expect(modelBuilderUxBaseUrl()).toBe("");
  });

  it("joins seed path without a trailing slash on the base", () => {
    expect(modelBuilderSeedActionUrl("http://localhost:3145/")).toBe(
      "http://localhost:3145/api/model-builder/seed"
    );
  });
});
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-handoff.test.ts
```

- [ ] **Step 3: Implement helpers + nav + page**

`model-builder-handoff.ts`:

```ts
export function modelBuilderUxBaseUrl(): string {
  return (process.env.BACKEND_MODEL_UX_BASE_URL ?? "").trim().replace(/\/+$/, "");
}

export function modelBuilderSeedActionUrl(baseUrl: string): string {
  return `${baseUrl.replace(/\/+$/, "")}/api/model-builder/seed`;
}
```

`loyalty-nav.tsx` — import `modelBuilderUxBaseUrl`. Replace the Model Builder span:

```tsx
      {modelBuilderUxBaseUrl() ? (
        <Link className={linkClass} href="/loyalty/models/handoff">
          Model Builder
        </Link>
      ) : (
        <span className={disabledClass}>Model Builder</span>
      )}
```

`handoff/page.tsx`:

```tsx
import { auth } from "@/auth";
import { redirect } from "next/navigation";
import { buildModelBuilderBrief } from "@/lib/model-builder-brief";
import { modelBuilderSeedActionUrl, modelBuilderUxBaseUrl } from "@/lib/model-builder-handoff";
import { resolveTenantId } from "@/lib/resolve-tenant-id";
import { getAllSchemas } from "@/services/loyalty/actions";

export default async function ModelBuilderHandoffPage() {
  const session = await auth();
  if (!session?.user) redirect("/signin");

  const base = modelBuilderUxBaseUrl();
  if (!base) {
    return (
      <p className="text-sm text-zinc-600">
        Model Builder is not configured. Set BACKEND_MODEL_UX_BASE_URL.
      </p>
    );
  }

  const tenantId = resolveTenantId((session as { tenantId?: string }).tenantId);
  const catalog = await getAllSchemas();
  const brief = buildModelBuilderBrief({
    tenantId,
    schemas: catalog.success ? catalog.data ?? [] : [],
    catalogUnavailable: !catalog.success
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight">Model Builder</h1>
      <p className="text-sm text-zinc-600">
        Opens Backend Modeler in a new tab for this tenant, with a one-time Journeys event-signal brief.
      </p>
      <form
        action={modelBuilderSeedActionUrl(base)}
        method="post"
        target="_blank"
        rel="noopener"
      >
        <input type="hidden" name="tenantId" value={tenantId} />
        <input type="hidden" name="source" value="journeys" />
        <input type="hidden" name="brief" value={brief} />
        <button
          type="submit"
          className="rounded-md bg-zinc-900 px-3 py-2 text-sm text-white"
        >
          Open Model Builder
        </button>
      </form>
    </div>
  );
}
```

Add to `.env.example`:

```
BACKEND_MODEL_UX_BASE_URL=
```

`journeys-ux.md`: add this spec to the Specs line; add a short **Model Builder** subsection: env, `/loyalty/models/handoff`, new tab, brief = contract + catalog gaps, ModelCache may need API restart after Backend save.

Shell spec NonGoals line: change `model builder` to `model builder (nav now: docs/specs/2026-09-18-Journeys-ux-backend-model-builder-handoff-design.md)`.

Waiver:

```md
# Waiver: Model Builder handoff

**Reason:** Nav opens Backend.Model.UX. No new capability. event-models stays IMPLEMENTED_AS Core/Backend, not proj-ux.

**Nodes:** `event-models`.
```

Run:

```powershell
cd C:\Dev\Journeys\Journeys
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.UX/src/lib/model-builder-brief.ts",
  "Journeys.UX/src/app/loyalty/models/handoff/page.tsx",
  "docs/developer/journeys-ux.md"
)
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.UX/src/lib/model-builder-brief.ts",
  "docs/developer/journeys-ux.md",
  "docs/product/graph/waivers/2026-09-18-model-builder-handoff.md"
)
```

- [ ] **Step 4: `npm test` in Journeys.UX — expect PASS**

- [ ] **Step 5: Do not commit** unless asked.

---

### Task 3: Backend.Model.UX tenant query + seed + first turn

**Files:**
- Modify: `C:\Dev\Backend\Backend.Model.UX\components\layout\tenant-context.tsx`
- Create: `C:\Dev\Backend\Backend.Model.UX\lib\journeys-seed.ts`
- Create: `C:\Dev\Backend\Backend.Model.UX\lib\journeys-seed.test.ts`
- Create: `C:\Dev\Backend\Backend.Model.UX\app\api\model-builder\seed\route.ts`
- Modify: `C:\Dev\Backend\Backend.Model.UX\components\model-coach\model-coach-client.tsx`
- Create: `C:\Dev\Backend\Backend.Model.UX\lib\coach-search-params.ts` (optional helper)

**Interfaces:**
- Consumes: existing `useTenant`, `ModelCoachClient.send`
- Produces:
  - `JOURNEYS_SEED_STORAGE_KEY = "backend-model-ux-journeys-brief"`
  - `takeJourneysSeedBrief(): string | null` — read + `removeItem` (browser only)
  - `writeJourneysSeedBriefScript(brief: string, tenantId: string): string` — escaped HTML that sets sessionStorage then `location.replace`
  - TenantProvider applies `?tenantId=` over localStorage default
  - Seed POST: parse `application/x-www-form-urlencoded` `tenantId`, `brief`, `source`. If `source !== "journeys"` or brief empty → 400. Else `Content-Type: text/html` body from `writeJourneysSeedBriefScript`
  - Coach: if `source=journeys` (from `useSearchParams`), `takeJourneysSeedBrief()` once and `sendMessage(brief)`

- [ ] **Step 1: Failing seed HTML + consume tests**

`journeys-seed.ts` must be importable from Node tests (no `window` at module load). `takeJourneysSeedBrief` uses `globalThis.sessionStorage` when present.

```ts
import { describe, expect, it } from "vitest";
import { writeJourneysSeedBriefScript } from "./journeys-seed";

describe("writeJourneysSeedBriefScript", () => {
  it("does not put the brief in the redirect query", () => {
    const html = writeJourneysSeedBriefScript("Never use modelId \"unknown\".", "acme");
    expect(html).toContain("sessionStorage");
    expect(html).toContain("source=journeys");
    expect(html).toContain("tenantId=acme");
    expect(html).not.toMatch(/location\.replace\([^)]*unknown/);
  });

  it("escapes a brief that contains </script>", () => {
    const html = writeJourneysSeedBriefScript("</script>alert(1)", "acme");
    expect(html).not.toContain("</script>alert");
  });
});
```

- [ ] **Step 2: Run — expect FAIL**

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test -- lib/journeys-seed.test.ts
```

- [ ] **Step 3: Implement seed + tenant + coach**

`lib/journeys-seed.ts`:

```ts
export const JOURNEYS_SEED_STORAGE_KEY = "backend-model-ux-journeys-brief";

export function takeJourneysSeedBrief(): string | null {
  if (typeof sessionStorage === "undefined") return null;
  const value = sessionStorage.getItem(JOURNEYS_SEED_STORAGE_KEY);
  sessionStorage.removeItem(JOURNEYS_SEED_STORAGE_KEY);
  return value && value.trim() ? value : null;
}

export function writeJourneysSeedBriefScript(brief: string, tenantId: string): string {
  const payload = JSON.stringify({ brief, tenantId });
  const safe = payload.replace(/</g, "\\u003c");
  const tenantQ = encodeURIComponent(tenantId);
  return `<!DOCTYPE html><html><body><script>
(function(){
  var p = ${safe};
  try { sessionStorage.setItem(${JSON.stringify(JOURNEYS_SEED_STORAGE_KEY)}, p.brief); } catch (e) {}
  location.replace("/models/coach?tenantId=" + encodeURIComponent(p.tenantId) + "&source=journeys");
})();
</script></body></html>`;
}
```

Do not interpolate `tenantQ` unused — use `p.tenantId` only as above.

`app/api/model-builder/seed/route.ts`:

```ts
import { NextResponse } from "next/server";
import { writeJourneysSeedBriefScript } from "@/lib/journeys-seed";

export async function POST(request: Request) {
  const form = await request.formData();
  const tenantId = String(form.get("tenantId") ?? "").trim();
  const brief = String(form.get("brief") ?? "").trim();
  const source = String(form.get("source") ?? "").trim();
  if (source !== "journeys" || !tenantId || !brief) {
    return NextResponse.json({ error: "tenantId, brief, and source=journeys are required" }, { status: 400 });
  }
  return new NextResponse(writeJourneysSeedBriefScript(brief, tenantId), {
    status: 200,
    headers: { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store" }
  });
}
```

`tenant-context.tsx`: on mount (client), if `window.location.search` has `tenantId`, `setTenantId` that value. Keep localStorage persist.

`model-coach-client.tsx`: extract the body of `send` into `sendMessage(userMsg: string)` used by `send`. Add:

```ts
  const seededRef = useRef(false);
  useEffect(() => {
    if (seededRef.current) return;
    if (typeof window === "undefined") return;
    const source = new URLSearchParams(window.location.search).get("source");
    if (source !== "journeys") return;
    const brief = takeJourneysSeedBrief();
    if (!brief) return;
    seededRef.current = true;
    void sendMessage(brief);
  }, [sendMessage]);
```

`sendMessage` must be `useCallback` with the same deps as today's `send`.

- [ ] **Step 4: Tests**

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test
```

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

- [ ] **Step 5: Do not commit** unless asked.

---

## Self-review (plan vs spec)

| Spec | Task |
|------|------|
| G1 nav / env disabled | Task 2 |
| G2 tenant query | Task 3 tenant-context |
| G3 one-time brief | Task 3 seed + take + source=journeys |
| G4 GetMany only | Task 1–2; no API writes |
| G5 safe payload | Task 1 tests |
| Form POST not query brief | Task 2 form + Task 3 HTML handshake |
| GetMany failure warning | Task 1 `catalogUnavailable` |
| Docs + waiver no proj-ux | Task 2 |
| Backend.Model.UX tests | Task 3 |

No C# tasks. Two repos: Tasks 1–2 Journeys, Task 3 Backend.Model.UX.
