import { describe, expect, it } from "vitest";

import { computeCampaignHealth } from "../compute-campaign-health";

describe("computeCampaignHealth", () => {
  it("flags missing name and open-ended schedule", () => {
    const report = computeCampaignHealth({
      name: "",
      status: "draft",
      startDate: "2026-01-01T00:00:00.000Z",
    });

    expect(report.warnings.some((issue) => issue.id === "missing-name")).toBe(true);
    expect(report.warnings.some((issue) => issue.id === "no-end-date")).toBe(true);
  });

  it("recommends outcomes when journey has no configured actions", () => {
    const report = computeCampaignHealth({
      name: "Promo",
      status: "draft",
      startDate: "2026-01-01T00:00:00.000Z",
      endDate: "2026-12-31T00:00:00.000Z",
      journey: {
        id: "root",
        name: "Root",
        rules: [],
        children: [],
      },
    });

    expect(report.recs.some((issue) => issue.id === "missing-outcomes")).toBe(true);
  });
});
