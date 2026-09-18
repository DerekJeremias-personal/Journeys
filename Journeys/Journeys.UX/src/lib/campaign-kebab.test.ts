import { describe, expect, it } from "vitest";
import { campaignKebabVisibility, kebabSaveStatus, normalizeCampaignStatus } from "./campaign-kebab";

describe("normalizeCampaignStatus", () => {
  it("maps EXP leftovers", () => {
    expect(normalizeCampaignStatus("active")).toBe("live");
    expect(normalizeCampaignStatus("archived")).toBe("archive");
    expect(normalizeCampaignStatus("LIVE")).toBe("live");
    expect(normalizeCampaignStatus("pause")).toBe("pause");
  });
});

describe("campaignKebabVisibility", () => {
  it("Draft: edit, duplicate, publish, delete; no agent, unpublish, archive, restore", () => {
    const v = campaignKebabVisibility("draft", "ext");
    expect(v).toMatchObject({
      edit: true, agent: false, duplicate: true, versions: true,
      publish: true, unpublish: false, archive: false, restore: false, delete: true
    });
  });
  it("Live: agent, unpublish, archive; no delete, publish, restore", () => {
    const v = campaignKebabVisibility("live", "ext");
    expect(v).toMatchObject({
      edit: true, agent: true, publish: false, unpublish: true,
      archive: true, restore: false, delete: false
    });
  });
  it("Pause: publish, archive; no agent", () => {
    const v = campaignKebabVisibility("pause", "ext");
    expect(v.agent).toBe(false);
    expect(v.publish).toBe(true);
    expect(v.archive).toBe(true);
  });
  it("Archive: restore only among mutations; no delete/agent", () => {
    const v = campaignKebabVisibility("archive", "ext");
    expect(v).toMatchObject({
      agent: false, restore: true, delete: false, archive: false, publish: false
    });
  });
  it("hides versions without extCampaignId", () => {
    expect(campaignKebabVisibility("live", undefined).versions).toBe(false);
  });
});

describe("kebabSaveStatus", () => {
  it("does not map unpublish to draft", () => {
    expect(kebabSaveStatus("unpublish")).toBe("pause");
    expect(kebabSaveStatus("publish")).toBe("live");
    expect(kebabSaveStatus("archive")).toBe("archive");
  });
});
