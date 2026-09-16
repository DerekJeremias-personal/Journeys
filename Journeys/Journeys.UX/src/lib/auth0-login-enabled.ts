export function auth0LoginEnabled(): boolean {
  return Boolean(
    process.env.AUTH0_CLIENT_ID?.trim() &&
      process.env.AUTH0_CLIENT_SECRET?.trim() &&
      process.env.AUTH0_ISSUER?.trim()
  );
}
