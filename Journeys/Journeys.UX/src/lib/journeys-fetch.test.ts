import { describe, expect, it, vi } from "vitest";
import { journeysFetch, type JourneysSession } from "./journeys-fetch";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

describe("journeysFetch", () => {
  it("does not call fetch when session is missing", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/getall", {}, {
      getSession: async () => null,
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.success).toBe(false);
    expect(r.error).toMatch(/Not authenticated/i);
  });

  it("does not call fetch when path is not allowlisted", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/save", {}, {
      getSession: async () => session(),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.error).toMatch(/not-allowlisted/);
  });

  it("POSTs getall with API key header and wraps entities", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ entities: [{ id: "c1", name: "N", status: "Live" }] }), { status: 200 })
    );
    const r = await journeysFetch("campaigns/ignored/getall", { method: "POST", body: { pageSize: 100 } }, {
      getSession: async () => session({ accessToken: undefined, apiKey: "secret-key" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("https://api.example/api/Campaign/acme/getall");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBe("secret-key");
    expect((init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
    expect(r.success).toBe(true);
  });

  it("sends Bearer when accessToken is present and does not send API key", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("schemas/x/model/all", { method: "POST", body: {} }, {
      getSession: async () => session({ accessToken: "tok", apiKey: "should-not-send" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example/"
    });
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect((init.headers as Record<string, string>)["Authorization"]).toBe("Bearer tok");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBeUndefined();
  });

  it("sends JOURNEYS_TENANT_ID even when the session tenant differs", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "TestTenant1");
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("campaigns/x/getall", { method: "POST", body: {} }, {
      getSession: async () => session({ tenantId: "stale-session-tenant" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    const [url] = fetchMock.mock.calls[0] as unknown as [string];
    expect(url).toBe("https://api.example/api/Campaign/TestTenant1/getall");
  });
});
