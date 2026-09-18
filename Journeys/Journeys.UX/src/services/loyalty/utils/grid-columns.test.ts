import { describe, expect, it } from "vitest";

import { generateColumnsFromSchema, getInitialSortFromSchema, getSchemaFieldValue } from "./grid-columns";

function schemaWithAttributes(attributes: unknown[]) {
  return {
    id: "schema-1",
    name: "LoyaltyAccountDetails",
    attributes,
  } as never;
}

describe("generateColumnsFromSchema", () => {
  it("uses configured grid attributes even when attribute status is Draft", () => {
    const columns = generateColumnsFromSchema(
      schemaWithAttributes([
        {
          type: "Primitive",
          symbol: "email",
          status: "Draft",
          dataType: "String",
          displayName: "Email",
          isInGrid: true,
          gridColumnNumber: 3,
        },
        {
          type: "Primitive",
          symbol: "profileid",
          status: "Draft",
          dataType: "String",
          displayName: "Profile ID",
          isInGrid: true,
          gridColumnNumber: 0,
        },
        {
          type: "Primitive",
          symbol: "internalnote",
          status: "Draft",
          dataType: "String",
          displayName: "Internal note",
        },
      ])
    );

    expect(columns.map((column) => column.key)).toEqual(["profileid", "email"]);
    expect(columns.map((column) => column.label)).toEqual(["Profile ID", "Email"]);
  });

  it("falls back to live primitive attributes when no grid attributes are configured", () => {
    const columns = generateColumnsFromSchema(
      schemaWithAttributes([
        {
          type: "Primitive",
          symbol: "firstName",
          status: "Live",
          dataType: "String",
          displayName: "First name",
        },
        {
          type: "Primitive",
          symbol: "draftOnly",
          status: "Draft",
          dataType: "String",
          displayName: "Draft only",
        },
      ])
    );

    expect(columns.map((column) => column.key)).toEqual(["firstName"]);
  });
});

describe("getInitialSortFromSchema", () => {
  it("uses configured draft grid attributes for initial sort", () => {
    const sort = getInitialSortFromSchema(
      schemaWithAttributes([
        {
          type: "Primitive",
          symbol: "profileid",
          status: "Draft",
          dataType: "String",
          displayName: "Profile ID",
          isInGrid: true,
          gridColumnNumber: 0,
          isGridSortable: true,
          defaultSortDirection: "asc",
        },
      ])
    );

    expect(sort).toEqual({ key: "profileid", order: "asc" });
  });
});

describe("getSchemaFieldValue", () => {
  it("reads exact and case-insensitive dynamic payload fields", () => {
    const row = {
      event: {
        orderid: "wxyz_101",
        ProfileID: "test_001",
      },
    };

    expect(getSchemaFieldValue(row, "event.orderid")).toBe("wxyz_101");
    expect(getSchemaFieldValue(row, "event.orderId")).toBe("wxyz_101");
    expect(getSchemaFieldValue(row, "event.profileId")).toBe("test_001");
  });
});
