import { describe, expect, it } from "vitest";

import {
  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
  resolveLoyaltyAccountId,
  resolveLoyaltyAccountXReference,
} from "./account-identifiers";

describe("loyalty account identifiers", () => {
  it("searches every account id field used by the account pickers", () => {
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.id = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.id = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.loyaltyAccountId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.loyaltyAccountId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.profileid = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.profileid = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.profileId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.profileId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.customerid = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.customerid = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.customerId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.customerId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.extAccountId = @id");
    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.extAccountId = @id");
  });

  it("prefers external profile/customer ids for display while preserving the account id fallback", () => {
    const entity = {
      id: "document-1",
      loyaltyAccountId: "loyalty-account-1",
      event: {
        profileid: "external-profile-1",
      },
    };

    expect(resolveLoyaltyAccountId(entity, "fallback-id")).toBe("loyalty-account-1");
    expect(resolveLoyaltyAccountXReference(entity, "fallback-id")).toBe("external-profile-1");
  });
});
