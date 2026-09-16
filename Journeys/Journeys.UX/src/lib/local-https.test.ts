import { afterEach, describe, expect, it } from "vitest";
import { allowInsecureLocalHttps, describeFetchFailure } from "./local-https";

describe("allowInsecureLocalHttps", () => {
  const previous = process.env.NODE_TLS_REJECT_UNAUTHORIZED;

  afterEach(() => {
    if (previous === undefined) delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    else process.env.NODE_TLS_REJECT_UNAUTHORIZED = previous;
  });

  it("sets NODE_TLS_REJECT_UNAUTHORIZED for localhost HTTPS", () => {
    delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    allowInsecureLocalHttps("https://localhost:7001");
    expect(process.env.NODE_TLS_REJECT_UNAUTHORIZED).toBe("0");
  });

  it("does not set NODE_TLS_REJECT_UNAUTHORIZED for a remote URL", () => {
    delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    allowInsecureLocalHttps("https://api.example.com");
    expect(process.env.NODE_TLS_REJECT_UNAUTHORIZED).toBeUndefined();
  });
});

describe("describeFetchFailure", () => {
  it("includes TLS cause code when present", () => {
    const err = new Error("fetch failed") as Error & { cause?: { code: string } };
    err.cause = { code: "DEPTH_ZERO_SELF_SIGNED_CERT" };
    expect(describeFetchFailure(err)).toBe("fetch failed: DEPTH_ZERO_SELF_SIGNED_CERT");
  });
});
