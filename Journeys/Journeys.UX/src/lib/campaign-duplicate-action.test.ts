import { describe, expect, it, vi } from "vitest";
import { duplicateCampaign } from "./campaign-duplicate-action";

describe("duplicateCampaign", () => {
  it("calls copyCampaign not updateCampaign", async () => {
    const copyCampaign = vi.fn(async () => ({ success: true, timestamp: "" }));
    const updateCampaign = vi.fn();
    await duplicateCampaign({
      id: "live-1",
      status: "live",
      copyCampaign,
      updateCampaign
    });
    expect(copyCampaign).toHaveBeenCalledWith("live-1", "live");
    expect(updateCampaign).not.toHaveBeenCalled();
  });
});
