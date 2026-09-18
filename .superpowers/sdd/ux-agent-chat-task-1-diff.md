# Review package Task 1
Base: uncommitted (no commits per human rule)
Head: working tree

## Files
diff --git a/Journeys.UX/src/lib/campaign-agent/stream-url.ts b/Journeys.UX/src/lib/campaign-agent/stream-url.ts
new file mode 100644
index 0000000..003b033
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/stream-url.ts
@@ -0,0 +1,7 @@
+export function campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string {
+  const tenant = tenantId.trim();
+  if (!tenant) throw new Error("tenantId is required");
+  const base = apiBaseUrl.trim().replace(/\/+$/, "");
+  if (!base) throw new Error("JOURNEYS_API_BASE_URL is not configured");
+  return `${base}/api/v1/${encodeURIComponent(tenant)}/campaign-agent/messages/stream`;
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/stream-url.test.ts b/Journeys.UX/src/lib/campaign-agent/stream-url.test.ts
new file mode 100644
index 0000000..2098126
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/stream-url.test.ts
@@ -0,0 +1,24 @@
+import { describe, expect, it } from "vitest";
+import { campaignAgentStreamUrl } from "./stream-url";
+
+describe("campaignAgentStreamUrl", () => {
+  it("maps to Journeys.API campaign-agent stream", () => {
+    expect(campaignAgentStreamUrl("https://127.0.0.1:7001", "TestTenant1")).toBe(
+      "https://127.0.0.1:7001/api/v1/TestTenant1/campaign-agent/messages/stream"
+    );
+  });
+
+  it("trims trailing slash on the base URL and encodes tenant", () => {
+    expect(campaignAgentStreamUrl("https://api.example/", "ac me")).toBe(
+      "https://api.example/api/v1/ac%20me/campaign-agent/messages/stream"
+    );
+  });
+
+  it("rejects empty tenantId", () => {
+    expect(() => campaignAgentStreamUrl("https://api.example", "  ")).toThrow(/tenant/i);
+  });
+
+  it("rejects empty base URL", () => {
+    expect(() => campaignAgentStreamUrl("  ", "acme")).toThrow(/JOURNEYS_API_BASE_URL|base/i);
+  });
+});
