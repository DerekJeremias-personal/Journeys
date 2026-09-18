import { describe, expect, it } from "vitest";
import { extractCampaignIdFromMcpData } from "./extract-campaign-id";

describe("extractCampaignIdFromMcpData", () => {
  it("extracts a campaign UUID", () => {
    expect(
      extractCampaignIdFromMcpData(
        JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })
      )
    ).toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
  });

  it("returns null when no campaign id exists", () => {
    expect(extractCampaignIdFromMcpData("{}")).toBeNull();
  });

  it("returns null for invalid JSON and non-UUID ids", () => {
    expect(extractCampaignIdFromMcpData("{")).toBeNull();
    expect(extractCampaignIdFromMcpData('{"campaignId":"campaign-1"}')).toBeNull();
  });
});
