import NextAuth from "next-auth";
import Auth0 from "next-auth/providers/auth0";
import Credentials from "next-auth/providers/credentials";
import { apiKeyLoginEnabled } from "@/lib/api-key-login-enabled";
import { auth0LoginEnabled } from "@/lib/auth0-login-enabled";
import { resolveTenantId } from "@/lib/resolve-tenant-id";

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
    ...(auth0LoginEnabled()
      ? [
          Auth0({
            clientId: process.env.AUTH0_CLIENT_ID,
            clientSecret: process.env.AUTH0_CLIENT_SECRET,
            issuer: process.env.AUTH0_ISSUER
          })
        ]
      : []),
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
      const fromUser = user?.tenantId;
      const resolved = resolveTenantId(typeof fromUser === "string" ? fromUser : token.tenantId);
      if (resolved) token.tenantId = resolved;
      if (user && "apiKey" in user && typeof user.apiKey === "string") token.apiKey = user.apiKey;
      if (account?.access_token) token.accessToken = account.access_token;
      return token;
    },
    async session({ session, token }) {
      session.tenantId = typeof token.tenantId === "string" ? token.tenantId : undefined;
      session.accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.apiKey = typeof token.apiKey === "string" ? token.apiKey : undefined;
      if (session.user && token.sub) session.user.id = token.sub;
      return session;
    }
  }
});
