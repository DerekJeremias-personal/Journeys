import { describe, expect, it } from "vitest";

import { computeAffectedEstimate } from "../compute-affected-estimate";
import { sliceMapViewModel, visibleNodeCountForPhase } from "../progressive-map-utils";

describe("computeAffectedEstimate", () => {
  it("returns a rounded estimate", () => {
    expect(
      computeAffectedEstimate({ eligibilityRuleCount: 1, criteriaRuleCount: 1, hasEventRule: false })
    ).toBeGreaterThan(420);
  });
});

describe("progressive map utils", () => {
  it("reveals more nodes as phases advance", () => {
    expect(visibleNodeCountForPhase("setup")).toBe(1);
    expect(visibleNodeCountForPhase("actions")).toBe(4);
  });

  it("slices core nodes", () => {
    const node = (id: string) => ({
      id,
      kind: "Campaign" as const,
      title: id,
      summary: null,
      journeyNodeId: id,
      core: true
    });
    const vm = {
      coreNodes: [node("1"), node("2"), node("3")],
      branches: []
    };
    expect(sliceMapViewModel(vm, 2).coreNodes).toHaveLength(2);
  });
});
