import type { ApiResponse } from "./api-types";
import { allowInsecureLocalHttps, describeFetchFailure } from "./local-https";
import { mapLoyaltyPath } from "./map-loyalty-path";
import { resolveTenantId } from "./resolve-tenant-id";
import { wrapApiEnvelope } from "./wrap-api-envelope";

export type JourneysSession = {
  userId: string;
  tenantId: string;
  accessToken?: string;
  apiKey?: string;
};

export type JourneysFetchOptions = {
  method?: string;
  body?: unknown;
};

export type JourneysFetchDeps = {
  getSession: () => Promise<JourneysSession | null>;
  fetch: typeof fetch;
  apiBaseUrl: string;
};

export async function journeysFetch<T>(
  path: string,
  options: JourneysFetchOptions = {},
  deps?: JourneysFetchDeps
): Promise<ApiResponse<T>> {
  const timestamp = new Date().toISOString();
  const resolved: JourneysFetchDeps = deps ?? {
    getSession: defaultGetSession,
    fetch,
    apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? ""
  };
  const session = await resolved.getSession();
  if (!session?.userId) {
    return { success: false, error: "Not authenticated", timestamp };
  }
  const tenantId = resolveTenantId(session.tenantId);
  if (!tenantId) {
    return { success: false, error: "TenantId is required", timestamp };
  }
  const base = resolved.apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) {
    return { success: false, error: "JOURNEYS_API_BASE_URL is not configured", timestamp };
  }

  let apiPath: string;
  try {
    apiPath = mapLoyaltyPath(path, tenantId);
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    return { success: false, error: message, timestamp };
  }

  const headers: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
  if (session.accessToken) {
    headers.Authorization = `Bearer ${session.accessToken}`;
  } else if (session.apiKey) {
    headers["Journeys-API-KEY"] = session.apiKey;
  } else {
    return { success: false, error: "No credentials in session", timestamp };
  }

  const method = options.method ?? "GET";
  if (!deps) allowInsecureLocalHttps(base);
  try {
    const res = await resolved.fetch(`${base}${apiPath}`, {
      method,
      headers,
      body: method === "GET" || options.body === undefined ? undefined : JSON.stringify(options.body),
      cache: "no-store"
    });
    const text = await res.text();
    return wrapApiEnvelope(res.status, text) as ApiResponse<T>;
  } catch (e) {
    return { success: false, error: `Cannot reach Journeys.API (${describeFetchFailure(e)})`, timestamp };
  }
}

async function defaultGetSession(): Promise<JourneysSession | null> {
  const { auth } = await import("@/auth");
  const s = await auth();
  if (!s?.user?.id) return null;
  return {
    userId: s.user.id,
    tenantId: (s as { tenantId?: string }).tenantId ?? "",
    accessToken: (s as { accessToken?: string }).accessToken,
    apiKey: (s as { apiKey?: string }).apiKey
  };
}
