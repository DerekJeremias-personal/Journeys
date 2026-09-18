import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), "loyalty-accounts-list-client.tsx"),
  "utf8"
);

describe("loyalty accounts list client", () => {
  it("does not link to an account model builder", () => {
    expect(source).not.toContain("/builder");
    expect(source).not.toContain("builder");
  });

  it("does not link to data explorer routes", () => {
    expect(source).not.toContain("data-explorer");
  });

  it("states the missing-schema case in one sentence with no call to action", () => {
    expect(source).toContain("The LoyaltyAccountDetails schema is missing or not Live.");
  });

  it("routes row clicks at the accounts detail route", () => {
    expect(source).toContain('detailRoutePath="/loyalty/accounts"');
  });
});
