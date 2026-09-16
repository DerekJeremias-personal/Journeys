import { describe, expect, it, vi } from "vitest";
import { auth0LoginEnabled } from "./auth0-login-enabled";

describe("auth0LoginEnabled", () => {
  it("is false when issuer is missing", () => {
    vi.stubEnv("AUTH0_CLIENT_ID", "id");
    vi.stubEnv("AUTH0_CLIENT_SECRET", "secret");
    vi.stubEnv("AUTH0_ISSUER", "");
    expect(auth0LoginEnabled()).toBe(false);
  });

  it("is true only when client id, secret, and issuer are set", () => {
    vi.stubEnv("AUTH0_CLIENT_ID", "id");
    vi.stubEnv("AUTH0_CLIENT_SECRET", "secret");
    vi.stubEnv("AUTH0_ISSUER", "https://example.us.auth0.com/");
    expect(auth0LoginEnabled()).toBe(true);
  });
});
