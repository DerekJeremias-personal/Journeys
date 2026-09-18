import { describe, expect, it } from "vitest";

import {
  createEmptyJourney,
  getActionsTargetNode,
  getCriteriaTargetNode,
  getEligibilityTargetNode,
  patchJourneyNodeInTree,
} from "../journey-builder-journey-utils";

describe("journey-builder-journey-utils", () => {
  it("returns the first child for eligibility targeting", () => {
    const root = createEmptyJourney();
    const child = { ...createEmptyJourney(), id: "child-1", name: "Eligibility" };
    root.children = [child];

    expect(getEligibilityTargetNode(root).id).toBe("child-1");
  });

  it("returns the second node for criteria targeting", () => {
    const root = createEmptyJourney();
    const child = { ...createEmptyJourney(), id: "child-1", name: "Eligibility" };
    const grandchild = { ...createEmptyJourney(), id: "child-2", name: "Criteria" };
    child.children = [grandchild];
    root.children = [child];

    expect(getCriteriaTargetNode(root).id).toBe("child-2");
  });

  it("returns the third node for actions targeting", () => {
    const root = createEmptyJourney();
    const child = { ...createEmptyJourney(), id: "child-1", name: "Eligibility" };
    const grandchild = { ...createEmptyJourney(), id: "child-2", name: "Criteria" };
    const great = { ...createEmptyJourney(), id: "child-3", name: "Actions" };
    grandchild.children = [great];
    child.children = [grandchild];
    root.children = [child];

    expect(getActionsTargetNode(root).id).toBe("child-3");
  });

  it("patches a node in the journey tree", () => {
    const root = createEmptyJourney();
    const child = { ...createEmptyJourney(), id: "child-1", name: "Before" };
    root.children = [child];

    const next = patchJourneyNodeInTree(root, "child-1", { name: "After" });
    expect(next.children?.[0]?.name).toBe("After");
  });
});
