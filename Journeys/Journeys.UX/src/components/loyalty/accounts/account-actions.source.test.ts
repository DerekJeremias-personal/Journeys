import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const read = (relative: string) => readFileSync(resolve(here, relative), "utf8");

const dropdown = read("account-actions-dropdown.tsx");
const journeyModal = read("account-journey-modal.tsx");
const tierModal = read("manage-tier-modal.tsx");
const actions = read("../../../services/loyalty/actions.ts");

describe("account journey and tier actions", () => {
  it("moves a tier with MoveTier only, with no journey fallback", () => {
    expect(actions).not.toContain("moveTierViaJourneyFallback");
    expect(tierModal).not.toContain("moveTierViaJourneyFallback");
    expect(actions).toContain("PreviewTierMove");
    expect(actions).toContain("MoveTier");
  });

  it("audits journey movement writes and leaves the preview unaudited", () => {
    const preview = actions.slice(actions.indexOf("PreviewTierMove"), actions.indexOf("MoveTier`"));
    expect(preview).not.toContain("audit:");
    expect(actions).toContain('action: "Move Tier"');
    expect(actions).toContain('JOURNEY_MOVEMENT = "Journey Movement"');
  });

  it("never sends an empty admin user id", () => {
    expect(actions).not.toContain('adminUserId: ""');
  });

  it("keeps the operator surface free of builder and data explorer links", () => {
    for (const source of [dropdown, journeyModal, tierModal]) {
      expect(source).not.toContain("data-explorer");
      expect(source).not.toContain("/builder");
    }
  });
});
