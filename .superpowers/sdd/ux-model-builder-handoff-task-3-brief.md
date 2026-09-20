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
  - `takeJourneysSeedBrief(): string | null` â€” read + `removeItem` (browser only)
  - `writeJourneysSeedBriefScript(brief: string, tenantId: string): string` â€” escaped HTML that sets sessionStorage then `location.replace`
  - TenantProvider applies `?tenantId=` over localStorage default
  - Seed POST: parse `application/x-www-form-urlencoded` `tenantId`, `brief`, `source`. If `source !== "journeys"` or brief empty â†’ 400. Else `Content-Type: text/html` body from `writeJourneysSeedBriefScript`
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

- [ ] **Step 2: Run â€” expect FAIL**

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

Do not interpolate `tenantQ` unused â€” use `p.tenantId` only as above.

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

