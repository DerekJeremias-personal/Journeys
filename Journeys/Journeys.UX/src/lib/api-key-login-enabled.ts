export function apiKeyLoginEnabled(): boolean {
  return process.env.JOURNEYS_UX_ALLOW_API_KEY_LOGIN === "true";
}
