import { apiKeyLoginEnabled } from "@/lib/api-key-login-enabled";
import { auth0LoginEnabled } from "@/lib/auth0-login-enabled";
import { SignInForm } from "./sign-in-form";

export default function SignInPage() {
  return (
    <SignInForm
      allowApiKey={apiKeyLoginEnabled()}
      allowAuth0={auth0LoginEnabled()}
      defaultTenantId={process.env.JOURNEYS_TENANT_ID?.trim() ?? ""}
    />
  );
}
