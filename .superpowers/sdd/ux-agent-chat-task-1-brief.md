### Task 1: Upstream stream URL

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/stream-url.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/stream-url.test.ts`

**Interfaces:**
- Consumes: spec Â§4 URL shape
- Produces: `campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from "vitest";
import { campaignAgentStreamUrl } from "./stream-url";

describe("campaignAgentStreamUrl", () => {
  it("maps to Journeys.API campaign-agent stream", () => {
    expect(campaignAgentStreamUrl("https://127.0.0.1:7001", "TestTenant1")).toBe(
      "https://127.0.0.1:7001/api/v1/TestTenant1/campaign-agent/messages/stream"
    );
  });

  it("trims trailing slash on the base URL and encodes tenant", () => {
    expect(campaignAgentStreamUrl("https://api.example/", "ac me")).toBe(
      "https://api.example/api/v1/ac%20me/campaign-agent/messages/stream"
    );
  });

  it("rejects empty tenantId", () => {
    expect(() => campaignAgentStreamUrl("https://api.example", "  ")).toThrow(/tenant/i);
  });

  it("rejects empty base URL", () => {
    expect(() => campaignAgentStreamUrl("  ", "acme")).toThrow(/JOURNEYS_API_BASE_URL|base/i);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
Working directory: `Journeys.UX`  
Expected: FAIL (module not found)

- [ ] **Step 3: Implement**

```ts
export function campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const base = apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) throw new Error("JOURNEYS_API_BASE_URL is not configured");
  return `${base}/api/v1/${encodeURIComponent(tenant)}/campaign-agent/messages/stream`;
}
```

- [ ] **Step 4: Run tests**

Run: `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
Expected: PASS

- [ ] **Step 5: Commit** â€” skip unless the user asks.

---