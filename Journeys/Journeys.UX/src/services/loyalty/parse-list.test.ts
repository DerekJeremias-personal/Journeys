import { describe, expect, it } from "vitest";
import {
  attributeNamesFromRows,
  extractContinuationToken,
  extractConversationIds,
  extractEntities,
  normalizeCampaignRow,
  normalizeSchema,
  pickLiveSchema
} from "./parse-list";

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

describe("extractConversationIds", () => {
  it("reads camelCase items.conversationId", () => {
    expect(extractConversationIds({ items: [{ conversationId: "abc" }] })).toEqual(["abc"]);
  });

  it("reads PascalCase Items.ConversationId", () => {
    expect(
      extractConversationIds({ Items: [{ ConversationId: "def" }] })
    ).toEqual(["def"]);
  });
});

describe("normalizeCampaignRow", () => {
  it("keeps the journey tree so account tier progress can read it", () => {
    const row = normalizeCampaignRow({
      id: "camp-1",
      name: "Tier campaign",
      journey: { id: "journey-1", rootNodeId: "root-1", children: [{ id: "tier-bronze" }] }
    });
    expect(row?.journey).toEqual({
      id: "journey-1",
      rootNodeId: "root-1",
      children: [{ id: "tier-bronze" }]
    });
  });

  it("reads a PascalCase journey and omits a non-object one", () => {
    expect(normalizeCampaignRow({ id: "camp-1", Journey: { id: "journey-1" } })?.journey).toEqual({
      id: "journey-1"
    });
    expect(normalizeCampaignRow({ id: "camp-1", journey: "nope" })?.journey).toBeUndefined();
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

describe("extractContinuationToken", () => {
  it("reads continuationToken", () => {
    expect(extractContinuationToken({ entities: [], continuationToken: "tok" })).toBe("tok");
  });
  it("reads ContinuationToken", () => {
    expect(extractContinuationToken({ Entities: [], ContinuationToken: "tok2" })).toBe("tok2");
  });
  it("returns null when missing", () => {
    expect(extractContinuationToken({ entities: [] })).toBeNull();
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
