import { describe, expect, it } from "vitest";
import {
  REQUIRED_EVENT_MODEL_METADATA_KEYS,
  buildModelBuilderBrief,
  includeInModelBuilderCatalog,
  missingEventModelMetadata
} from "./model-builder-brief";
import type { SchemaListItem } from "./api-types";

const order: SchemaListItem = {
  id: "guid-1",
  name: "OrderPlaced",
  status: "Live",
  modelType: "loyalty",
  tag: "eventable",
  modelMetaData: { NaturalKeySymbols: "[\"orderid\"]" }
};

describe("model-builder-brief", () => {
  it("lists the four required metadata names in the contract", () => {
    const brief = buildModelBuilderBrief({ tenantId: "acme", schemas: [] });
    for (const key of REQUIRED_EVENT_MODEL_METADATA_KEYS) {
      expect(brief).toContain(key);
    }
    expect(brief).toContain('Never use modelId "unknown".');
  });

  it("includes eventable and LoyaltyAccountDetails; omits other tags", () => {
    expect(includeInModelBuilderCatalog(order)).toBe(true);
    expect(
      includeInModelBuilderCatalog({
        name: "LoyaltyAccountDetails",
        modelType: "loyalty"
      })
    ).toBe(true);
    expect(
      includeInModelBuilderCatalog({
        name: "Other",
        modelType: "loyalty",
        tag: "reference"
      })
    ).toBe(false);
  });

  it("flags missing Wrapper and does not embed journey JSON", () => {
    expect(missingEventModelMetadata(order)).toEqual([
      "Wrapper",
      "AccountXIdSymbol",
      "TimeOfOccurrence"
    ]);
    const brief = buildModelBuilderBrief({
      tenantId: "acme",
      schemas: [order, { name: "Other", journey: { id: "nope" } } as SchemaListItem]
    });
    expect(brief).toContain("OrderPlaced");
    expect(brief).toContain("missing: Wrapper");
    expect(brief).not.toContain("\"journey\"");
    expect(brief).not.toContain('id=unknown');
  });

  it("adds catalog warning when GetMany failed", () => {
    expect(
      buildModelBuilderBrief({ tenantId: "acme", schemas: [], catalogUnavailable: true })
    ).toContain("Catalog snapshot unavailable");
  });
});
