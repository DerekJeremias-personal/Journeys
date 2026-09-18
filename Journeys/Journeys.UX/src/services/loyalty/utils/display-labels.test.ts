import { describe, expect, it } from "vitest";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";

import { formatDisplayName, getEntityDisplayName, getSchemaDisplayName } from "./display-labels";

describe("formatDisplayName", () => {
  it("formats schema names without losing known initialisms", () => {
    expect(formatDisplayName("order")).toBe("Order");
    expect(formatDisplayName("orderId")).toBe("Order ID");
    expect(formatDisplayName("customer_profile")).toBe("Customer Profile");
  });
});

describe("getSchemaDisplayName", () => {
  it("prefers configured display name and formats raw names", () => {
    expect(getSchemaDisplayName({ name: "order", displayName: undefined, attributes: [] })).toBe("Order");
    expect(getSchemaDisplayName({ name: "order", displayName: "Sales Order", attributes: [] })).toBe("Sales Order");
  });
});

describe("getEntityDisplayName", () => {
  it("uses human identifiers before raw UUID ids", () => {
    const schema = {
      name: "order",
      attributes: [
        {
          symbol: "orderId",
          displayName: "Order ID",
          type: "Primitive",
          dataType: "String",
          status: "Live",
        },
      ],
    } as LoyaltySchema;

    expect(
      getEntityDisplayName(
        schema,
        {
          id: "6f7610a4-78f1-46cb-8a6d-6d3a6ed26469",
          event: { orderid: "wxyz_101" },
        },
        "6f7610a"
      )
    ).toBe("wxyz_101");
  });
});
