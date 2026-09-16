import type { ApiResponse } from "./api-types";

function nowIso(): string {
  return new Date().toISOString();
}

export function wrapApiEnvelope(status: number, bodyText: string): ApiResponse<unknown> {
  const timestamp = nowIso();
  const trimmed = bodyText.trim();
  let parsed: unknown;
  if (trimmed) {
    try {
      parsed = JSON.parse(trimmed) as unknown;
    } catch {
      parsed = undefined;
    }
  }

  if (status >= 200 && status < 300) {
    return { success: true, data: trimmed ? parsed : undefined, timestamp };
  }

  let error = `HTTP ${status}`;
  if (parsed && typeof parsed === "object" && parsed !== null && "error" in parsed) {
    const e = (parsed as { error: unknown }).error;
    if (typeof e === "string" && e.trim()) error = e;
  } else if (!parsed && trimmed) {
    error = `HTTP ${status}: ${trimmed.slice(0, 180)}`;
  }
  return { success: false, error, timestamp };
}
