import { describe, expect, it, vi } from "vitest";
import { apiKeyLoginEnabled } from "./api-key-login-enabled";

describe("apiKeyLoginEnabled", () => {
  it("is false when unset", () => {
    vi.stubEnv("JOURNEYS_UX_ALLOW_API_KEY_LOGIN", "");
    expect(apiKeyLoginEnabled()).toBe(false);
  });
  it("is true only for exact true", () => {
    vi.stubEnv("JOURNEYS_UX_ALLOW_API_KEY_LOGIN", "true");
    expect(apiKeyLoginEnabled()).toBe(true);
  });
});
