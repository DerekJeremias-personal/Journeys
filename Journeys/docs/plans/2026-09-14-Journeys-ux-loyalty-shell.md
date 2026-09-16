# Journeys.UX Loyalty Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This repo’s human rule wins: do not start tasks until the user approves this plan. Prefer a fresh subagent per task.

**Goal:** Add a standalone Next.js app `Journeys.UX` with Auth0 plus Development API-key login, Loyalty-only nav, and read-only Accounts + Campaigns lists over HTTP to `Journeys.API`.

**Architecture:** Surgical, simple client. `journeysFetch` keeps the EXP `bffFetch("loyalty", path)` call shape but allowlists three Journeys.API routes and wraps raw DTOs as `{ success, data, error }`. No Prisma, no EXP BFF, no Core/DAL project references. UI is HTML-simple (not a full shadcn/EXP shell clone).

**Tech Stack:** Next.js 16, React 19, NextAuth 5, Zod, Vitest, TypeScript. Existing `Journeys.API` net8.0.

**Spec:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md`

## Global Constraints

- Do not invent capability ids. Existing ids only: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- `Journeys.UX` must not project-reference Core, DAL, or Infra. HTTP to `Journeys.API` only.
- Do not copy the EXP monorepo, `@exp/prisma-*`, Aerospike, Experiences, or CDP.
- Do not change Auth0 tenant `"hayward"` in `Journeys.Infra.Auth`.
- Product `TenantId` is session/env `JOURNEYS_TENANT_ID`, not the Auth0 key `"hayward"`.
- Do not implement Campaign Agent UI, SSE, Journey Builder, Ollama, Playwright, or list mutations.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Never log tokens, API keys, connection strings, or full event payloads.
- `JOURNEYS_UX_ALLOW_API_KEY_LOGIN` must be exactly the string `true` to show API-key sign-in.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.UX/package.json` | Next 16 app scripts and deps |
| `Journeys.UX/.env.example` | Empty placeholders for secrets |
| `Journeys.UX/src/lib/api-types.ts` | `ApiResponse<T>`, campaign/schema row types |
| `Journeys.UX/src/lib/map-loyalty-path.ts` | Allowlist EXP-shaped path → Journeys.API path |
| `Journeys.UX/src/lib/wrap-api-envelope.ts` | Raw HTTP → `{ success, data, error }` |
| `Journeys.UX/src/lib/journeys-fetch.ts` | Session + fetch; fail closed |
| `Journeys.UX/src/auth.ts` | NextAuth Auth0 + optional api-key credentials |
| `Journeys.UX/src/app/api/auth/[...nextauth]/route.ts` | Auth handlers |
| `Journeys.UX/src/proxy.ts` | Gate `/loyalty` |
| `Journeys.UX/src/app/signin/page.tsx` | Auth0 button + flagged API-key form |
| `Journeys.UX/src/app/loyalty/layout.tsx` | Loyalty-only nav |
| `Journeys.UX/src/app/loyalty/page.tsx` | Overview stub |
| `Journeys.UX/src/app/loyalty/campaigns/page.tsx` | Read-only campaign list |
| `Journeys.UX/src/app/loyalty/accounts/page.tsx` | Read-only accounts table |
| `Journeys.UX/src/services/loyalty/actions.ts` | `getCampaigns`, `getSchemaByName`, `queryData` |
| `docs/developer/journeys-ux.md` | How to run the UX |
| `docs/product/graph/nodes.yaml` / `edges.yaml` / `path-map.yaml` | `proj-ux`; retire `ui-in-this-sln` |

---

### Task 1: Scaffold `Journeys.UX`

**Files:**
- Create: `Journeys.UX/package.json`
- Create: `Journeys.UX/tsconfig.json`
- Create: `Journeys.UX/next.config.ts`
- Create: `Journeys.UX/vitest.config.ts`
- Create: `Journeys.UX/.env.example`
- Create: `Journeys.UX/src/app/layout.tsx`
- Create: `Journeys.UX/src/app/page.tsx`
- Create: `Journeys.UX/src/app/globals.css`
- Modify: `.gitignore` (append `Journeys.UX/.env.local` if missing)

**Interfaces:**
- Consumes: spec §3 placement
- Produces: `npm run dev` and `npm test` scripts later tasks use from `Journeys.UX/`

- [ ] **Step 1: Create `Journeys.UX/package.json`**

```json
{
  "name": "journeys-ux",
  "version": "0.0.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "next dev --port 3000",
    "build": "next build",
    "start": "next start",
    "test": "vitest run"
  },
  "dependencies": {
    "next": "^16.0.7",
    "next-auth": "5.0.0-beta.30",
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "zod": "^4.0.0"
  },
  "devDependencies": {
    "@types/node": "^22.0.0",
    "@types/react": "^19.0.0",
    "@types/react-dom": "^19.0.0",
    "typescript": "^5.6.0",
    "vitest": "^3.0.0"
  }
}
```

- [ ] **Step 2: Create `Journeys.UX/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["dom", "dom.iterable", "es2022"],
    "allowJs": false,
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true,
    "esModuleInterop": true,
    "module": "esnext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "jsx": "preserve",
    "incremental": true,
    "plugins": [{ "name": "next" }],
    "paths": { "@/*": ["./src/*"] }
  },
  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
  "exclude": ["node_modules"]
}
```

- [ ] **Step 3: Create `Journeys.UX/next.config.ts`**

```ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true
};

export default nextConfig;
```

- [ ] **Step 4: Create `Journeys.UX/vitest.config.ts`**

```ts
import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  test: { environment: "node" },
  resolve: { alias: { "@": resolve(root, "src") } }
});
```

- [ ] **Step 5: Create `Journeys.UX/.env.example`** (empty values only)

```
AUTH_SECRET=
AUTH_TRUST_HOST=true
AUTH0_CLIENT_ID=
AUTH0_CLIENT_SECRET=
AUTH0_ISSUER=
JOURNEYS_API_BASE_URL=https://localhost:7001
JOURNEYS_TENANT_ID=
JOURNEYS_UX_ALLOW_API_KEY_LOGIN=false
JOURNEYS_UX_TLS_REJECT_UNAUTHORIZED=true
```

- [ ] **Step 6: Create `Journeys.UX/src/app/globals.css`**

```css
:root { font-family: system-ui, sans-serif; color: #111; }
body { margin: 0; }
a { color: inherit; }
button { cursor: pointer; }
.layout { display: flex; min-height: 100vh; }
nav.loyalty-nav { width: 240px; padding: 1rem; background: #f4f4f5; }
nav.loyalty-nav h1 { font-size: 1rem; margin: 0 0 1rem; }
nav.loyalty-nav a, nav.loyalty-nav span { display: block; padding: 0.35rem 0; }
nav.loyalty-nav .disabled { color: #888; }
main { padding: 1.5rem; flex: 1; }
.card-row { display: flex; gap: 1rem; }
.card { border: 1px solid #ddd; padding: 1rem; min-width: 12rem; }
.error { color: #b91c1c; }
.empty { color: #555; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ddd; padding: 0.4rem 0.6rem; text-align: left; }
```

- [ ] **Step 7: Create `Journeys.UX/src/app/layout.tsx` and `src/app/page.tsx`**

```tsx
import type { ReactNode } from "react";
import "./globals.css";

export const metadata = { title: "Journeys" };

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
```

```tsx
import { redirect } from "next/navigation";

export default function Home() {
  redirect("/loyalty");
}
```

- [ ] **Step 8: Append to `.gitignore` if not already present**

```
Journeys.UX/.env.local
Journeys.UX/.next
```

- [ ] **Step 9: Install and typecheck**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm install
npx tsc --noEmit
```

Expected: exit 0 (or only missing `next-env.d.ts` until first `next dev`; if so run `npx next build` is not required yet — `npx next dev` for one second is enough to emit it, or add an empty `Journeys.UX/next-env.d.ts` with the Next reference comment).

- [ ] **Step 10: Do not commit** unless the user asks in that message.

---

### Task 2: Allowlist path map + envelope (TDD)

**Files:**
- Create: `Journeys.UX/src/lib/map-loyalty-path.ts`
- Create: `Journeys.UX/src/lib/wrap-api-envelope.ts`
- Create: `Journeys.UX/src/lib/api-types.ts`
- Test: `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- Test: `Journeys.UX/src/lib/wrap-api-envelope.test.ts`

**Interfaces:**
- Consumes: spec §5 allowlist
- Produces: `mapLoyaltyPath(path: string, tenantId: string): string` throws `Error` with message starting `not-allowlisted:` when unknown. `wrapApiEnvelope(status: number, bodyText: string): ApiResponse<unknown>`

- [ ] **Step 1: Create `Journeys.UX/src/lib/api-types.ts`**

```ts
export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
  timestamp: string;
};

export type CampaignListItem = {
  id?: string;
  name?: string;
  status?: string;
  extCampaignId?: string;
};

export type SchemaListItem = {
  id?: string;
  name?: string;
  status?: string;
  attributes?: { name?: string; displayName?: string }[];
};
```

- [ ] **Step 2: Write failing tests `Journeys.UX/src/lib/map-loyalty-path.test.ts`**

```ts
import { describe, expect, it } from "vitest";
import { mapLoyaltyPath } from "./map-loyalty-path";

describe("mapLoyaltyPath", () => {
  it("maps campaigns getall using session tenant, not path slug", () => {
    expect(mapLoyaltyPath("campaigns/slug-from-exp/getall", "acme")).toBe(
      "/api/Campaign/acme/getall"
    );
  });

  it("maps schemas model/all", () => {
    expect(mapLoyaltyPath("schemas/slug/model/all", "acme")).toBe(
      "/api/Model/acme/GetMany"
    );
  });

  it("maps events admin query and preserves schema name", () => {
    expect(mapLoyaltyPath("events/slug/LoyaltyAccountDetails/admin/query", "acme")).toBe(
      "/api/Events/acme/LoyaltyAccountDetails/admin/query"
    );
  });

  it("rejects unknown paths", () => {
    expect(() => mapLoyaltyPath("campaigns/slug/save", "acme")).toThrow(/not-allowlisted:/);
  });

  it("rejects empty tenantId", () => {
    expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
  });
});
```

- [ ] **Step 3: Write failing tests `Journeys.UX/src/lib/wrap-api-envelope.test.ts`**

```ts
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
```

- [ ] **Step 4: Run tests — expect FAIL**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Expected: FAIL — modules not found.

- [ ] **Step 5: Implement `map-loyalty-path.ts` and `wrap-api-envelope.ts`**

```ts
export function mapLoyaltyPath(path: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const trimmed = path.replace(/^\/+/, "");

  if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
  }
  if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
    return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
  }
  const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
  if (events) {
    return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
  }
  throw new Error(`not-allowlisted: ${trimmed}`);
}
```

```ts
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
```

- [ ] **Step 6: Re-run `npm test` — expect PASS**

- [ ] **Step 7: Do not commit** unless the user asks in that message.

---

### Task 3: `journeysFetch` (TDD)

**Files:**
- Create: `Journeys.UX/src/lib/journeys-fetch.ts`
- Test: `Journeys.UX/src/lib/journeys-fetch.test.ts`

**Interfaces:**
- Consumes: `mapLoyaltyPath`, `wrapApiEnvelope`
- Produces: `journeysFetch<T>(path: string, options?: JourneysFetchOptions, deps?: JourneysFetchDeps): Promise<ApiResponse<T>>`
- `JourneysFetchOptions`: `{ method?: string; body?: unknown }`
- `JourneysSession`: `{ userId: string; tenantId: string; accessToken?: string; apiKey?: string }`
- Missing session → `{ success: false, error: "Not authenticated" }` (no network)
- Missing tenantId → `{ success: false, error: "TenantId is required" }` (no network)
- Auth header: if `accessToken` set, `Authorization: Bearer ${accessToken}` only; else if `apiKey` set, `Journeys-API-KEY: ${apiKey}` only; else `{ success: false, error: "No credentials in session" }`
- `JOURNEYS_API_BASE_URL` from `deps.apiBaseUrl` or `process.env.JOURNEYS_API_BASE_URL`; missing → `{ success: false, error: "JOURNEYS_API_BASE_URL is not configured" }`
- Not-allowlisted path → `{ success: false, error }` containing `not-allowlisted` (no network)

- [ ] **Step 1: Write `journeys-fetch.test.ts`**

```ts
import { describe, expect, it, vi } from "vitest";
import { journeysFetch, type JourneysSession } from "./journeys-fetch";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

describe("journeysFetch", () => {
  it("does not call fetch when session is missing", async () => {
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/getall", {}, {
      getSession: async () => null,
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.success).toBe(false);
    expect(r.error).toMatch(/Not authenticated/i);
  });

  it("does not call fetch when path is not allowlisted", async () => {
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/save", {}, {
      getSession: async () => session(),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.error).toMatch(/not-allowlisted/);
  });

  it("POSTs getall with API key header and wraps entities", async () => {
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ entities: [{ id: "c1", name: "N", status: "Live" }] }), { status: 200 })
    );
    const r = await journeysFetch("campaigns/ignored/getall", { method: "POST", body: { pageSize: 100 } }, {
      getSession: async () => session({ accessToken: undefined, apiKey: "secret-key" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example/api/Campaign/acme/getall");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBe("secret-key");
    expect((init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
    expect(r.success).toBe(true);
  });

  it("sends Bearer when accessToken is present and does not send API key", async () => {
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("schemas/x/model/all", { method: "POST", body: {} }, {
      getSession: async () => session({ accessToken: "tok", apiKey: "should-not-send" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example/"
    });
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>)["Authorization"]).toBe("Bearer tok");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBeUndefined();
  });
});
```

- [ ] **Step 2: `npm test` — expect FAIL on `journeysFetch`**

- [ ] **Step 3: Implement `journeys-fetch.ts`**

```ts
import type { ApiResponse } from "./api-types";
import { mapLoyaltyPath } from "./map-loyalty-path";
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
  if (!session.tenantId?.trim()) {
    return { success: false, error: "TenantId is required", timestamp };
  }
  const base = resolved.apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) {
    return { success: false, error: "JOURNEYS_API_BASE_URL is not configured", timestamp };
  }

  let apiPath: string;
  try {
    apiPath = mapLoyaltyPath(path, session.tenantId);
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
    const message = e instanceof Error ? e.message : String(e);
    return { success: false, error: `Cannot reach Journeys.API (${message})`, timestamp };
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
```

If the `defaultGetSession` dynamic import of `@/auth` makes Vitest load NextAuth during Task 3 tests, keep tests passing by always injecting `deps` (they already do). Do not import `@/auth` at module top level in this file.

- [ ] **Step 4: `npm test` — expect PASS**

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

### Task 4: NextAuth (Auth0 + flagged API-key)

**Files:**
- Create: `Journeys.UX/src/auth.ts`
- Create: `Journeys.UX/src/types/next-auth.d.ts`
- Create: `Journeys.UX/src/app/api/auth/[...nextauth]/route.ts`
- Create: `Journeys.UX/src/proxy.ts`
- Create: `Journeys.UX/src/app/signin/page.tsx`
- Test: `Journeys.UX/src/lib/api-key-login-enabled.test.ts`

**Interfaces:**
- Consumes: env names from `.env.example`
- Produces: `auth`, `handlers`, `signIn`, `signOut` from `src/auth.ts`. Session fields `tenantId`, optional `accessToken`, optional `apiKey`. Credentials provider id is `api-key`. `apiKeyLoginEnabled()` is `process.env.JOURNEYS_UX_ALLOW_API_KEY_LOGIN === "true"`.

- [ ] **Step 1: Write `api-key-login-enabled.test.ts`**

```ts
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
```

- [ ] **Step 2: Implement `src/lib/api-key-login-enabled.ts`**

```ts
export function apiKeyLoginEnabled(): boolean {
  return process.env.JOURNEYS_UX_ALLOW_API_KEY_LOGIN === "true";
}
```

- [ ] **Step 3: `npm test` — PASS for this file after implementation. Run tests after step 2.**

- [ ] **Step 4: Create `src/types/next-auth.d.ts`**

```ts
import "next-auth";

declare module "next-auth" {
  interface Session {
    tenantId?: string;
    accessToken?: string;
    apiKey?: string;
  }
  interface User {
    tenantId?: string;
    apiKey?: string;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    tenantId?: string;
    accessToken?: string;
    apiKey?: string;
  }
}
```

- [ ] **Step 5: Create `src/auth.ts`**

```ts
import NextAuth from "next-auth";
import Auth0 from "next-auth/providers/auth0";
import Credentials from "next-auth/providers/credentials";
import { apiKeyLoginEnabled } from "@/lib/api-key-login-enabled";

const apiKeyProvider = Credentials({
  id: "api-key",
  name: "API key",
  credentials: {
    apiKey: { label: "API key", type: "password" },
    tenantId: { label: "Tenant id", type: "text" }
  },
  async authorize(credentials) {
    const apiKey = typeof credentials?.apiKey === "string" ? credentials.apiKey.trim() : "";
    const tenantId = typeof credentials?.tenantId === "string" ? credentials.tenantId.trim() : "";
    if (!apiKey || !tenantId) return null;
    return { id: "api-key-user", tenantId, apiKey };
  }
});

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  pages: { signIn: "/signin" },
  providers: [
    Auth0({
      clientId: process.env.AUTH0_CLIENT_ID,
      clientSecret: process.env.AUTH0_CLIENT_SECRET,
      issuer: process.env.AUTH0_ISSUER
    }),
    ...(apiKeyLoginEnabled() ? [apiKeyProvider] : [])
  ],
  callbacks: {
    authorized({ auth: session, request: { nextUrl } }) {
      const isLoggedIn = !!session?.user;
      if (nextUrl.pathname.startsWith("/api/auth") || nextUrl.pathname === "/signin") return true;
      if (nextUrl.pathname.startsWith("/loyalty") || nextUrl.pathname === "/") return isLoggedIn;
      return true;
    },
    async jwt({ token, user, account }) {
      const configuredTenant = process.env.JOURNEYS_TENANT_ID?.trim();
      if (user?.tenantId) token.tenantId = user.tenantId;
      else if (!token.tenantId && configuredTenant) token.tenantId = configuredTenant;
      if (user && "apiKey" in user && typeof user.apiKey === "string") token.apiKey = user.apiKey;
      if (account?.access_token) token.accessToken = account.access_token;
      return token;
    },
    async session({ session, token }) {
      session.tenantId = token.tenantId;
      session.accessToken = token.accessToken;
      session.apiKey = token.apiKey;
      if (session.user && token.sub) session.user.id = token.sub;
      return session;
    }
  }
});
```

- [ ] **Step 6: Create `src/app/api/auth/[...nextauth]/route.ts`**

```ts
import { handlers } from "@/auth";

export const { GET, POST } = handlers;
```

- [ ] **Step 7: Create `src/proxy.ts` (Next 16 Auth wrapper; same as EXP `proxy.ts`)**

```ts
import { auth } from "@/auth";

export default auth;
export const config = { matcher: ["/loyalty/:path*", "/"] };
```

If `next-auth` 5 + Next 16 in this tree expects `middleware.ts` instead of `proxy.ts`, use `middleware.ts` exporting `export { auth as middleware } from "@/auth"` with the same `config.matcher`. Do not implement both.

- [ ] **Step 8: Create `src/app/signin/page.tsx` as a client component**

- Title: `Sign in to Journeys` (not EXP).
- Always render a button that calls `signIn("auth0", { callbackUrl: "/loyalty" })`.
- If `apiKeyLoginEnabled()` is true, also render a form: fields `tenantId`, `apiKey`; submit `signIn("api-key", { apiKey, tenantId, callbackUrl: "/loyalty", redirect: true })`.
- Because `apiKeyLoginEnabled` reads `process.env`, expose it to the client via a server wrapper: `src/app/signin/page.tsx` is a **server** component that passes `allowApiKey={apiKeyLoginEnabled()}` into `src/app/signin/sign-in-form.tsx` (`"use client"`). Do not put the API key into any HTML default value.

- [ ] **Step 9: `npm test` — PASS. `npx tsc --noEmit` — PASS.**

- [ ] **Step 10: Do not commit** unless the user asks in that message.

---

### Task 5: Loyalty nav + Overview

**Files:**
- Create: `Journeys.UX/src/app/loyalty/layout.tsx`
- Create: `Journeys.UX/src/app/loyalty/page.tsx`
- Create: `Journeys.UX/src/components/loyalty-nav.tsx`

**Interfaces:**
- Consumes: session (layout may call `auth()` and redirect to `/signin` if missing)
- Produces: nav items listed below

Nav items (exact labels/hrefs). Live: Overview `/loyalty`, Accounts `/loyalty/accounts`, Campaigns `/loyalty/campaigns`. Disabled (`<span className="disabled">`, not a link): Promotions, Analytics, Action Log, Notifications, File Ingestion, Settings, Data Explorer, Model Builder.

- [ ] **Step 1: Implement `loyalty-nav.tsx` as a server component with those items.** First heading text: `Loyalty`.

- [ ] **Step 2: `loyalty/layout.tsx` wraps children with `.layout` > nav + `<main>`.**

- [ ] **Step 3: `loyalty/page.tsx` title `Loyalty`. Two `.card` links only: Accounts and Campaigns.**

- [ ] **Step 4: Do not add routes for disabled items.** Visiting `/loyalty/promotions` should 404.

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

### Task 6: Loyalty actions + Campaigns + Accounts pages

**Files:**
- Create: `Journeys.UX/src/services/loyalty/actions.ts`
- Create: `Journeys.UX/src/app/loyalty/campaigns/page.tsx`
- Create: `Journeys.UX/src/app/loyalty/accounts/page.tsx`
- Test: `Journeys.UX/src/services/loyalty/parse-list.test.ts`

**Interfaces:**
- Consumes: `journeysFetch`
- Produces:
  - `getCampaigns(): Promise<ApiResponse<CampaignListItem[]>>` — `journeysFetch("campaigns/{tenant}/getall", { method: "POST", body: { pageSize: 100, continuationToken: null } })` then `extractEntities`
  - `getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>>` — POST `schemas/{tenant}/model/all` with body `{}` then normalize array or `{ items|Items: [] }`
  - `getSchemaByName(name: string)` — `getAllSchemas()`, case-insensitive name match AND `status` case-insensitive `"live"`
  - `queryData(schemaName: string): Promise<ApiResponse<Record<string, unknown>[]>>` — POST `events/{tenant}/{schemaName}/admin/query` with body `{ query: "", parameters: {}, pageSize: 50, continuationToken: null, sortBy: "", sortOrder: "ASC" }` then `extractEntities`
- `extractEntities(data: unknown): unknown[]` — if `Array.isArray(data)` return it; if `data.entities` or `data.Entities` array return that; if `data.items` or `data.Items` array return that; else `[]`

Constant: `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME = "LoyaltyAccountDetails"`

- [ ] **Step 1: Write `parse-list.test.ts` for `extractEntities` and `pickLiveSchema(schemas, name)`** (implement those two functions in `src/services/loyalty/parse-list.ts` — keep `actions.ts` as `"use server"` wrappers only).

```ts
import { describe, expect, it } from "vitest";
import { extractEntities, pickLiveSchema } from "./parse-list";

describe("extractEntities", () => {
  it("returns arrays as-is", () => {
    expect(extractEntities([{ a: 1 }])).toEqual([{ a: 1 }]);
  });
  it("reads entities", () => {
    expect(extractEntities({ entities: [{ id: "1" }] })).toEqual([{ id: "1" }]);
  });
  it("reads Items", () => {
    expect(extractEntities({ Items: [{ id: "2" }] })).toEqual([{ id: "2" }]);
  });
});

describe("pickLiveSchema", () => {
  it("matches live name case-insensitively", () => {
    const s = pickLiveSchema(
      [
        { name: "LoyaltyAccountDetails", status: "Live" },
        { name: "LoyaltyAccountDetails", status: "Draft" }
      ],
      "loyaltyaccountdetails"
    );
    expect(s?.status).toBe("Live");
  });
  it("returns null when missing", () => {
    expect(pickLiveSchema([], "LoyaltyAccountDetails")).toBeNull();
  });
});
```

- [ ] **Step 2: Implement `parse-list.ts` so tests PASS.** `pickLiveSchema` returns the first item where `name` equals (case-insensitive) and `status === "Live"` (case-insensitive).

- [ ] **Step 3: Implement `"use server"` `actions.ts` calling `journeysFetch` + parse helpers.** Use path templates with a dummy slug `session` — `mapLoyaltyPath` ignores the slug. Example path string: `campaigns/session/getall`.

- [ ] **Step 4: Campaigns page (server component)**  
  Call `getCampaigns()`. On `!success` show `<p className="error">` with `error` (or “not authorized / check tenant or key” when error looks like 401/403 / nope). On empty array show `<p className="empty">No campaigns found.</p>`. Else a `<ul>` of `name` — `status` — `id`. No buttons.

- [ ] **Step 5: Accounts page (server component)**  
  `getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`. If null: `<p className="empty">The LoyaltyAccountDetails schema is missing or not Live for this tenant.</p>` — **no** builder link. If schema ok, `queryData(schema.name ?? LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`. Render `<table>`: column headers from `schema.attributes` `displayName ?? name`, cells from row `[attr.name]`. If attributes missing/empty, columns `id` and `name` only. Empty rows: `No loyalty accounts found.`

- [ ] **Step 6: `npm test` PASS. `npx tsc --noEmit` PASS.**

- [ ] **Step 7: Do not commit** unless the user asks in that message.

---

### Task 7: Docs, graph, onion overlay

**Files:**
- Create: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/index.md`
- Modify: `docs/developer/local-ops.md`
- Modify: `docs/platform/overlays.md`
- Modify: `docs/platform/architecture.md` (layers table UI row; add `Journeys.UX` to the center→edge sentence as HTTP client, not in the C# arrow)
- Modify: `docs/roadmap/non-goals.md`
- Modify: `docs/product/graph/nodes.yaml`
- Modify: `docs/product/graph/edges.yaml`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`
- Modify: `AGENTS.md` first paragraph
- Modify: `.cursor/rules/onion-architecture.mdc` UI bullet

**Interfaces:**
- Consumes: spec §10 exact graph rules
- Produces: `proj-ux` node; `ui-in-this-sln` **removed** from `nodes.yaml`; new edge `proj-ux` `CONSTRAINED_BY` `tenant-id-required`; path-map prefix `Journeys.UX` nodes `[campaigns]` `meaningOptional: false`

- [ ] **Step 1: Create `docs/developer/journeys-ux.md` with this exact body**

```markdown
# Journeys.UX

Standalone Next.js admin (React) for Loyalty lists. Open `Journeys.UX/` in its own Cursor window. It talks **HTTP only** to `Journeys.API`. It is not a C# project and must not reference Core, DAL, or Infra.

**Spec:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md`

## Run

1. `Journeys.API` listening (typical `https://localhost:7001`).
2. Copy `Journeys.UX/.env.example` to `Journeys.UX/.env.local` and fill values. Never commit `.env.local`.
3. `JOURNEYS_TENANT_ID` is the product tenant id (not Auth0 `"hayward"`).
4. `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true` only on a trusted machine. Then sign-in can use `Journeys-API-KEY`.
5. From `Journeys.UX`: `npm install` then `npm run dev` (port 3000).
6. Open `/signin`, then `/loyalty`.

If Node fetch fails TLS to local HTTPS, set `NODE_TLS_REJECT_UNAUTHORIZED=0` **only in that shell** for local API. Do not set it in committed files. `JOURNEYS_UX_TLS_REJECT_UNAUTHORIZED` in `.env.example` is documentation; do not read it to disable TLS in production builds.

## This spec’s screens

Overview, Accounts table, Campaigns list. Other Loyalty nav items are disabled on purpose.
```

- [ ] **Step 2: Add bullet on `docs/developer/index.md`:** `- [Journeys.UX](journeys-ux.md) — Loyalty admin Next app`

- [ ] **Step 3: Append to `docs/developer/local-ops.md` a subsection `## Journeys.UX` pointing at `journeys-ux.md`.**

- [ ] **Step 4: Overlays — add row:** `| UI | \`{Product}.UX\` | \`Journeys.UX\` (HTTP to API only; not a csproj) |`

- [ ] **Step 5: Architecture layers table UI cell:** change “Not in this solution” to `Journeys.UX` with Must not: `Project-reference Core or Adapters. Own writes of campaigns/accounts.` Keep “A UI never becomes the write authority.”

- [ ] **Step 6: `docs/roadmap/non-goals.md` — delete the UI/`ui-in-this-sln` bullet. Keep “Copying EXP monorepo files”.**

- [ ] **Step 7: Graph** — delete node `ui-in-this-sln`. Add:

```yaml
  - id: proj-ux
    label: Project
    name: Journeys.UX
```

Delete edge `from: ui-in-this-sln` `NOT_YET` `campaigns` if present. Append:

```yaml
  - from: proj-ux
    type: CONSTRAINED_BY
    to: tenant-id-required
```

`path-map.yaml` top of `entries:`:

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns]
    meaningOptional: false
```

`scripts/path-docs-map.yaml` top:

```yaml
  - prefix: Journeys.UX
    docs:
      - docs/developer/journeys-ux.md
      - docs/platform/architecture.md
      - docs/platform/overlays.md
```

- [ ] **Step 8: `AGENTS.md` first paragraph** — replace the last sentence so it states: this folder is the onion .NET solution (`Journeys.sln`); `Journeys.UX` is a sibling Next.js app (HTTP to `Journeys.API` only), not a C# project.

- [ ] **Step 9: `.cursor/rules/onion-architecture.mdc`** — replace “UI is not in this solution. Do not add a web frontend here.” with: `Journeys.UX` is the HTTP-only Next.js UI. Do not project-reference Core/DAL/Infra from it. Do not put a web frontend inside the C# onion projects.

- [ ] **Step 10: Confirm no new capability ids**

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern 'id:' | ForEach-Object { $_.Line }
```

Expected: existing seven capabilities plus `proj-ux`; **no** `ui-in-this-sln`.

- [ ] **Step 11: Do not commit** unless the user asks in that message.

---

### Task 8: Verify impact scripts + local smoke

**Files:** none new.

- [ ] **Step 1: From `C:\Dev\Journeys\Journeys`**

```powershell
$files = @(
  "Journeys.UX/package.json",
  "docs/developer/journeys-ux.md",
  "docs/platform/architecture.md",
  "docs/platform/overlays.md",
  "docs/product/graph/nodes.yaml",
  "docs/product/graph/edges.yaml",
  "docs/product/graph/path-map.yaml",
  "scripts/path-docs-map.yaml"
)
.\scripts\docs-impact.ps1 -Files $files
.\scripts\graph-impact.ps1 -Files $files
```

Expected: both exit 0.

- [ ] **Step 2: `dotnet build .\Journeys.sln`** — exit 0 (UX is not in the sln).

- [ ] **Step 3: `cd Journeys.UX; npm test`** — all PASS.

- [ ] **Step 4: Manual (human):** API up, `.env.local` with `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true`, sign-in with key + tenant, open Overview, Campaigns, Accounts.

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

## Spec coverage (self-review)

| Spec | Task |
|------|------|
| G1 isolated Next app | 1 |
| G2 Auth0 + API-key flag | 4 |
| G3 Loyalty-only nav, disabled children | 5 |
| G4 campaigns getall | 2, 3, 6 |
| G5 accounts schema + queryData | 2, 3, 6 |
| G6 no EXP platform | all (no prisma/BFF) |
| Envelope + allowlist tests | 2, 3 |
| docs/graph/overlays/AGENTS/onion rule | 7 |
| agent-verify docs/graph + sln build | 8 |
| Non-goals (agent, Ollama, Playwright) | none implemented |
