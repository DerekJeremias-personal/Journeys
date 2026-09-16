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
