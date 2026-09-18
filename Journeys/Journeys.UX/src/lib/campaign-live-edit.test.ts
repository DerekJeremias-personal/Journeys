import { describe, expect, it, vi } from "vitest";
import { applyWizardSaveIdentity, resolveWizardCampaign } from "./campaign-live-edit";

describe("resolveWizardCampaign", () => {
  it("live with existing draft → edit that draft id", async () => {
    const getCampaign = vi.fn(async () => ({
      success: true,
      data: { id: "live-1", extCampaignId: "ext-1", status: "live" }
    }));
    const getDraftByExt = vi.fn(async () => ({
      success: true,
      data: { id: "draft-9" }
    }));

    const result = await resolveWizardCampaign({
      requested: { id: "live-1", status: "live" },
      getCampaign,
      getDraftByExt
    });

    expect(result).toEqual({ mode: "edit", id: "draft-9", status: "draft" });
    expect(getCampaign).toHaveBeenCalledWith("live-1", "live");
    expect(getDraftByExt).toHaveBeenCalledWith("ext-1");
  });

  it("live with no draft → new-draft-same-ext", async () => {
    const getCampaign = vi.fn(async () => ({
      success: true,
      data: { id: "live-1", extCampaignId: "ext-1", status: "live" }
    }));
    const getDraftByExt = vi.fn(async () => ({
      success: false,
      data: null
    }));

    const result = await resolveWizardCampaign({
      requested: { id: "live-1", status: "LIVE" },
      getCampaign,
      getDraftByExt
    });

    expect(result).toEqual({ mode: "new-draft-same-ext", extCampaignId: "ext-1" });
  });

  it("draft request → edit as draft", async () => {
    const getCampaign = vi.fn();
    const getDraftByExt = vi.fn();

    const result = await resolveWizardCampaign({
      requested: { id: "draft-3", status: "Draft" },
      getCampaign,
      getDraftByExt
    });

    expect(result).toEqual({ mode: "edit", id: "draft-3", status: "draft" });
    expect(getCampaign).not.toHaveBeenCalled();
    expect(getDraftByExt).not.toHaveBeenCalled();
  });
});

describe("applyWizardSaveIdentity", () => {
  it("new-draft omits id, forces draft, and pins ext over a mutated form value", () => {
    const result = applyWizardSaveIdentity(
      {
        id: "live-1",
        extCampaignId: "mutated-from-form",
        status: "live",
        etag: "etag-live"
      },
      "new-draft-same-ext",
      { extCampaignId: "ext-1" }
    );

    expect(result).not.toHaveProperty("id");
    expect(result.id).toBeUndefined();
    expect(result.status).toBe("draft");
    expect(result.extCampaignId).toBe("ext-1");
    expect(result).not.toHaveProperty("etag");
    expect(result.etag).toBeUndefined();
  });

  it("edit leaves id", () => {
    const result = applyWizardSaveIdentity(
      { id: "draft-3", extCampaignId: "ext-1", status: "draft", etag: "etag-draft" },
      "edit"
    );

    expect(result.id).toBe("draft-3");
    expect(result.extCampaignId).toBe("ext-1");
    expect(result.status).toBe("draft");
    expect(result.etag).toBe("etag-draft");
  });

  it("edit of live cannot flip to draft on the same id", () => {
    const result = applyWizardSaveIdentity(
      { id: "live-1", extCampaignId: "ext-1", status: "draft" },
      "edit",
      { sourceStatus: "live" }
    );

    expect(result.id).toBe("live-1");
    expect(result.status).toBe("live");
  });

  it("edit of live may pause, never archive on the same id", () => {
    const paused = applyWizardSaveIdentity(
      { id: "live-1", status: "pause" },
      "edit",
      { sourceStatus: "live" }
    );
    expect(paused.status).toBe("pause");

    const archived = applyWizardSaveIdentity(
      { id: "live-1", status: "archive" },
      "edit",
      { sourceStatus: "live" }
    );
    expect(archived.status).toBe("live");
  });
});
