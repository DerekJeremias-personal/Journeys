import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), "eventable-models-section.tsx"),
  "utf8"
);

describe("eventable models section", () => {
  it("does not link to data explorer routes", () => {
    expect(source).not.toContain("data-explorer");
  });

  it("does not link to an account model builder", () => {
    expect(source).not.toContain("builder");
  });

  it("queries events through the account-scoped queryData params", () => {
    expect(source).toContain("loyaltyAccountId,");
    expect(source).not.toContain("queryAdminData");
  });

  it("never sends an unknown model id", () => {
    expect(source).not.toContain('"unknown"');
  });
});
