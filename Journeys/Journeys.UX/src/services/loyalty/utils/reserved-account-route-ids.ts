/**
 * Segments that `/loyalty/accounts/[id]` must not treat as an account id. The
 * account model builder is deliberately not shipped, so the dynamic segment has
 * to refuse it instead of rendering an empty detail page for id "builder".
 */
const RESERVED_ACCOUNT_ROUTE_IDS = ["builder"];

export function isReservedAccountRouteId(id: string): boolean {
  return RESERVED_ACCOUNT_ROUTE_IDS.includes(id.trim().toLowerCase());
}
