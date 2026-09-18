import { describe, expect, it } from "vitest";
import { authorizeCampaignAgentProxy, campaignAgentUpstreamHeaders } from "./proxy-auth";
import type { JourneysSession } from "../journeys-fetch";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

describe("authorizeCampaignAgentProxy", () => {
  it("rejects missing session without calling upstream", () => {
    const r = authorizeCampaignAgentProxy(null);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.status).toBe(401);
  });

  it("rejects session with no credentials", () => {
    const r = authorizeCampaignAgentProxy(session({ apiKey: undefined, accessToken: undefined }));
    expect(r.ok).toBe(false);
  });

  it("accepts API-key session", () => {
    const r = authorizeCampaignAgentProxy(session());
    expect(r.ok).toBe(true);
  });
});

describe("campaignAgentUpstreamHeaders", () => {
  it("sends Journeys-API-KEY when there is no access token", () => {
    const h = campaignAgentUpstreamHeaders(session({ apiKey: "secret-key", accessToken: undefined }));
    expect(h["Journeys-API-KEY"]).toBe("secret-key");
    expect(h.Authorization).toBeUndefined();
    expect(h.Accept).toBe("text/event-stream");
  });

  it("sends Bearer and omits API key when accessToken is present", () => {
    const h = campaignAgentUpstreamHeaders(session({ accessToken: "tok", apiKey: "should-not-send" }));
    expect(h.Authorization).toBe("Bearer tok");
    expect(h["Journeys-API-KEY"]).toBeUndefined();
  });
});
