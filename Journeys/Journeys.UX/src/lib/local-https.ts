/** Relax TLS only for a local Journeys.API URL. Postman typically skips cert verify; Node fetch does not. */
export function allowInsecureLocalHttps(apiBaseUrl: string): void {
  let hostname = "";
  try {
    hostname = new URL(apiBaseUrl).hostname;
  } catch {
    return;
  }
  if (hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1") {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";
  }
}

export function describeFetchFailure(error: unknown): string {
  if (!(error instanceof Error)) return String(error);
  const cause = (error as Error & { cause?: { code?: string } }).cause;
  if (cause?.code) return `${error.message}: ${cause.code}`;
  return error.message;
}
