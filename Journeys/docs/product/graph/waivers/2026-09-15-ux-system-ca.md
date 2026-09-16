# Waiver: trust ASP.NET dev cert from Node

**Reason:** Node fetch to local HTTPS fails with `DEPTH_ZERO_SELF_SIGNED_CERT` (Postman typically skips SSL verify). System CA injection via `tls.setDefaultCACertificates` does not fix this self-signed ASP.NET cert. Local-only `NODE_TLS_REJECT_UNAUTHORIZED=0` plus `NODE_OPTIONS=--use-system-ca` on `npm run dev`. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `campaigns`.
