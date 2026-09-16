"use client";

import { FormEvent, useState } from "react";
import { signIn } from "next-auth/react";

export function SignInForm({
  allowApiKey,
  allowAuth0,
  defaultTenantId
}: {
  allowApiKey: boolean;
  allowAuth0: boolean;
  defaultTenantId: string;
}) {
  const [tenantId, setTenantId] = useState(defaultTenantId);
  const [apiKey, setApiKey] = useState("");

  function onApiKeySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void signIn("api-key", { apiKey, tenantId, callbackUrl: "/loyalty", redirect: true });
  }

  return (
    <main>
      <h1>Sign in to Journeys</h1>
      {allowAuth0 ? (
        <button type="button" onClick={() => signIn("auth0", { callbackUrl: "/loyalty" })}>
          Sign in with Auth0
        </button>
      ) : null}
      {allowApiKey ? (
        <form onSubmit={onApiKeySubmit}>
          <label>
            Tenant id
            <input
              name="tenantId"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              autoComplete="username"
            />
          </label>
          <label>
            API key
            <input
              name="apiKey"
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              autoComplete="off"
            />
          </label>
          <button type="submit">Sign in with API key</button>
        </form>
      ) : null}
    </main>
  );
}
