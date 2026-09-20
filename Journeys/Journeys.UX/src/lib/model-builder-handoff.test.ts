import { describe, expect, it, vi } from "vitest";
import {
  modelBuilderNavHref,
  modelBuilderSeedActionUrl,
  modelBuilderUxBaseUrl,
} from "./model-builder-handoff";

describe("model-builder-handoff", () => {
  it("returns empty base when unset", () => {
    vi.stubEnv("BACKEND_MODEL_UX_BASE_URL", "");
    expect(modelBuilderUxBaseUrl()).toBe("");
  });

  it("joins seed path without a trailing slash on the base", () => {
    expect(modelBuilderSeedActionUrl("http://localhost:3145/")).toBe(
      "http://localhost:3145/api/model-builder/seed"
    );
  });

  it("returns the handoff href when the Model Builder URL is set", () => {
    expect(modelBuilderNavHref("http://localhost:3145")).toBe("/loyalty/models/handoff");
  });

  it("returns null when the Model Builder URL is unset so nav stays a disabled span", () => {
    expect(modelBuilderNavHref("")).toBeNull();
    expect(modelBuilderNavHref("   ")).toBeNull();
  });
});
