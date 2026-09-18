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
