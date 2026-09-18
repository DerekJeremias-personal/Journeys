import { describe, expect, it, vi } from "vitest";
import { resolveTenantId } from "./resolve-tenant-id";

describe("resolveTenantId", () => {
  it("prefers JOURNEYS_TENANT_ID over a stale session tenant", () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "TestTenant1");
    expect(resolveTenantId("stale-session-tenant")).toBe("testtenant1");
  });

  it("uses the session tenant when env is empty", () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    expect(resolveTenantId("From-Form")).toBe("from-form");
  });

  it("lowercases so HTTP matches persistence partition keys", () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "TestTenant1");
    expect(resolveTenantId("ignored")).toBe("testtenant1");
  });
});
