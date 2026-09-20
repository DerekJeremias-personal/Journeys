**Collaborator:** aidlc-devsecops-agent

## Contribution

DevSecOps review of the practices-discovery lead draft for intent `mvp-engine-closeout`. Scope is lint/format enforcement, SAST/DAST, secret and dependency scanning, and supply-chain controls. No Journeys.* product code. No new ALWAYS/NEVER invented.

**Lint and format.** The Code Style draft is correct for this increment. C# enforcement is the coding-standards docs plus “do not reformat files you did not change,” not a second aesthetic. No `.editorconfig` and no `Directory.Build.props` are recorded in the inspected set. Org’s Prettier/ESLint-in-CI default does not apply here: `Journeys.UX` is out of increment, and this tree has no in-tree CI job that could run a linter as a merge gate. Do not add a formatter or analyzer config as a closeout practice. Do not promote org “linter failure blocks the PR” until an interview names a real pipeline.

**In-tree quality gate vs security scanning.** Evidence and Deployment are consistent: no `.github/` under the onion folder, no `azure-pipelines.yml`, no terraform/Databricks. The only recorded local gate is `scripts/aidlc-agent-verify-sensor.ps1` (docs-impact, graph-impact, then build). That script is a Construction completeness gate, not SAST, DAST, secret scanning, or dependency scanning. Keep the existing ALWAYS on the verify script. Do not treat it as a substitute for security scanners.

**SAST / DAST.** None observed in the inspected sources. Uncertainty item 6 is the right posture: ask whether scanning lives in another repo or is absent. Do not add a SAST or DAST ALWAYS to `discovered-rules.md`. Do not stand up a scanner for this Core closeout. DAST against local `https://localhost:7001` or Azure-backed adapters is out of increment.

**Secrets.** Human-stated controls already match `docs/platform/security.md` and `docs/platform/runtime.md`:

- Committed `appsettings*.json` hold empty keys only.
- Live credentials are user secrets, environment, or gitignored `appsettings.Local.json`.
- Azure Blob / Data Lake / Service Bus / Cosmos saga / key-value connections come from config or DI, not source.
- NEVER log secrets, full event payloads, webhook auth headers, or raw audit JSON is already a Forbidden rule and should stay.
- Auth0 tenant `"hayward"` is human-gated debt; NEVER edit it is already recorded and should stay.
- Auth, ledger/money-like outcomes, and tenant isolation stay human-gated.

Do not invent a secret-scanning ALWAYS. Absence of gitleaks/trufflehog/GitHub secret scanning in this tree is an interview fact, not a licence to add a tool.

**Dependency and supply-chain.** `dependencies.md` records inward onion refs and HintPath `Backend.Dto` / `Backend.*` DLLs. There is no in-tree Dependabot, NuGet audit gate, lockfile policy, or SBOM practice in the inspected set. Cross-package constraint “no Azure SDK or Backend client in Core” is already an affirmed style/onion rule and is the supply-chain control that matters for this increment. Notification webhook secrets stay on `NotificationConfig`; adapters must not log auth headers. Do not add email/Twilio/data_pipeline packages. Do not take a new `Journeys.Tests` → `Journeys.Notification` reference. Interview should confirm whether HintPath Backend binaries and NuGet restore are reviewed or pinned outside this folder. Until that is named, do not write a dependency-scan ALWAYS.

**Deployment / CD.** Org deploy-on-merge to staging plus manual production approval is an org default, not evidenced here. Agree with the draft: staging/prod CD location and who presses deploy are interview items. Merge and release remain human-only. Skip AWS/CDK and terraform/Databricks unless the human names infrastructure outside this sln.

**Discovered-rules set (security slice).** Mandated TenantId, human-gated auth/ledger/tenant isolation, NEVER log secrets, NEVER edit Auth0 `"hayward"`, NEVER merge/release as an agent, NEVER add terraform/Databricks, NEVER run AWS/CDK unless named — all stay. Increment non-goals (no hosted sweep, no `/points/expire`) stay in evidence, not as durable security rules.

**Interview items this spoke still needs (do not invent answers):**

1. Where, if anywhere, SAST, secret scanning, and dependency/vulnerability scanning run (other repo, host pipeline, or absent).
2. Where staging/prod CD lives and who approves production.
3. Whether HintPath `Backend.*` binaries have an attested restore or pin outside this tree.
4. Confirm no new lint/format or scanner config in this increment.

## Positions

- AGREE: No in-tree CI, SAST, DAST, secret scanning, or dependency scanning is recorded; keep that as interview uncertainty, not a new ALWAYS/NEVER.
- AGREE: Construction verify remains `scripts/aidlc-agent-verify-sensor.ps1`; it is not a security scanner.
- AGREE: C# lint/format stays coding-standards docs; no `.editorconfig` / `Directory.Build.props`; do not invent a formatter for this increment; UX Prettier/ESLint is out of increment.
- AGREE: Secrets via user secrets / environment / gitignored `appsettings.Local.json`; committed appsettings stay empty-key only.
- AGREE: NEVER log secrets, full event payloads, webhook auth headers, or raw audit JSON.
- AGREE: NEVER edit Auth0 tenant `"hayward"`; auth, ledger/money-like outcomes, and tenant isolation stay human-gated.
- AGREE: ALWAYS keep TenantId on every business operation.
- AGREE: No terraform/Databricks in this tree; skip AWS/CDK unless the human names work outside this sln.
- AGREE: Merge and release stay human-only; org deploy-on-merge is not evidenced and must not be affirmed from this tree.
- AGREE: Increment non-goals (no hosted sweep, no `/points/expire`) stay out of `discovered-rules.md`.
- AGREE: No Azure SDK or Backend client in Core; notification secrets stay on `NotificationConfig`; no new notification-adapter packages.
- OBJECT: Promoting org “linter in CI blocks the PR,” an 80% coverage-as-security-proxy, SAST/DAST, secret scanning, NuGet/SBOM audit, or deploy-on-merge as team/project practice without a named external pipeline or human affirmation.
- OBJECT: Adding scanner, formatter, or CI workflow files as a practices outcome of this closeout.
