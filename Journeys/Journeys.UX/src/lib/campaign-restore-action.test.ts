import { describe, expect, it, vi } from "vitest";
import { restoreArchived } from "./campaign-restore-action";

describe("restoreArchived", () => {
  it("calls restore endpoint not save-as-draft", async () => {
    const restoreCampaign = vi.fn(async () => ({ success: true, timestamp: "" }));
    const updateCampaign = vi.fn();
    await restoreArchived({ id: "a1", restoreCampaign, updateCampaign });
    expect(restoreCampaign).toHaveBeenCalledWith("a1");
    expect(updateCampaign).not.toHaveBeenCalled();
  });
});
