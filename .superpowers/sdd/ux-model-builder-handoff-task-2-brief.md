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
  - `modelBuilderUxBaseUrl(): string` â€” `process.env.BACKEND_MODEL_UX_BASE_URL?.trim().replace(/\/+$/, "") ?? ""`
  - `modelBuilderSeedActionUrl(baseUrl: string): string` â€” `${baseUrl}/api/model-builder/seed`
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

- [ ] **Step 2: Run â€” expect FAIL**

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

`loyalty-nav.tsx` â€” import `modelBuilderUxBaseUrl`. Replace the Model Builder span:

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

- [ ] **Step 4: `npm test` in Journeys.UX â€” expect PASS**

- [ ] **Step 5: Do not commit** unless asked.

---

