import { describe, expect, it } from "vitest";
import { attributeNamesFromRows, extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";

describe("extractEntities", () => {
  it("returns arrays as-is", () => {
    expect(extractEntities([{ a: 1 }])).toEqual([{ a: 1 }]);
  });
  it("reads entities", () => {
    expect(extractEntities({ entities: [{ id: "1" }] })).toEqual([{ id: "1" }]);
  });
  it("reads Items", () => {
    expect(extractEntities({ Items: [{ id: "2" }] })).toEqual([{ id: "2" }]);
  });
});

describe("normalizeSchema", () => {
  it("reads PascalCase Name/Status/ModelType", () => {
    const s = normalizeSchema({
      ID: "guid-1",
      Name: "LoyaltyAccountDetails",
      Status: "Live",
      ModelType: "loyalty"
    });
    expect(s).toEqual({
      id: "guid-1",
      name: "LoyaltyAccountDetails",
      status: "Live",
      modelType: "loyalty",
      attributes: undefined
    });
  });
});

describe("pickLiveSchema", () => {
  it("matches live name case-insensitively", () => {
    const s = pickLiveSchema(
      [
        { name: "LoyaltyAccountDetails", status: "Live" },
        { name: "LoyaltyAccountDetails", status: "Draft" }
      ],
      "loyaltyaccountdetails"
    );
    expect(s?.status).toBe("Live");
  });
  it("returns null when missing", () => {
    expect(pickLiveSchema([], "LoyaltyAccountDetails")).toBeNull();
  });
  it("skips non-loyalty modelType", () => {
    expect(
      pickLiveSchema(
        [{ name: "LoyaltyAccountDetails", status: "Live", modelType: "event" }],
        "LoyaltyAccountDetails"
      )
    ).toBeNull();
  });
});

describe("attributeNamesFromRows", () => {
  it("uses keys from the first row", () => {
    expect(attributeNamesFromRows([{ id: "1", name: "A" }])).toEqual([
      { name: "id" },
      { name: "name" }
    ]);
  });
  it("falls back when there are no rows", () => {
    expect(attributeNamesFromRows([])).toEqual([{ name: "id" }, { name: "name" }]);
  });
});
