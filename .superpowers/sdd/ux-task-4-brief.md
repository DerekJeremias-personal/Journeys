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

