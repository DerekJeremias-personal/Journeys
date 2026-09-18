import { describe, expect, it } from "vitest";
import { wrapApiEnvelope } from "./wrap-api-envelope";

describe("wrapApiEnvelope", () => {
  it("wraps a JSON array as success data", () => {
    const r = wrapApiEnvelope(200, JSON.stringify([{ id: "1" }]));
    expect(r.success).toBe(true);
    expect(r.data).toEqual([{ id: "1" }]);
  });

  it("wraps a JSON object as success data", () => {
    const r = wrapApiEnvelope(200, JSON.stringify({ entities: [] }));
    expect(r.success).toBe(true);
    expect((r.data as { entities: unknown[] }).entities).toEqual([]);
  });

  it("maps HTTP error JSON error field", () => {
    const r = wrapApiEnvelope(401, JSON.stringify({ error: "nope" }));
    expect(r.success).toBe(false);
    expect(r.error).toBe("nope");
  });

  it("maps HTTP error JSON errors object", () => {
    const r = wrapApiEnvelope(400, JSON.stringify({ errors: { status: "Cannot update Live" } }));
    expect(r.success).toBe(false);
    expect(r.error).toBe("Cannot update Live");
  });

  it("maps non-JSON error body to a short error", () => {
    const r = wrapApiEnvelope(500, "<html>fail</html>");
    expect(r.success).toBe(false);
    expect(r.error).toMatch(/HTTP 500/);
  });

  it("maps empty 200 body as success with undefined data", () => {
    const r = wrapApiEnvelope(200, "");
    expect(r.success).toBe(true);
    expect(r.data).toBeUndefined();
  });
});
