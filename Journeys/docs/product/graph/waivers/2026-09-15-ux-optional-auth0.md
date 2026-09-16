# Waiver: Auth0 optional for local API-key login

**Reason:** NextAuth was registering Auth0 with empty `AUTH0_ISSUER`, which failed `assertConfig` (`InvalidEndpoints`) and blocked API-key sign-in. Auth0 is now registered only when client id, secret, and issuer are all set. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `campaigns`.
