"use client";

import type { QueryClient } from "@tanstack/react-query";

import { loyaltyKeys } from "@/services/loyalty/query-keys";

/**
 * A point write moves the balance, the ledger, and any eventable rows keyed to
 * the same account, so all three are refetched after deposit / spend / expire.
 */
export function invalidatePointQueries(queryClient: QueryClient, loyaltyAccountId: string): void {
  void queryClient.invalidateQueries({
    queryKey: loyaltyKeys.points.balancesByAccount(loyaltyAccountId),
  });
  void queryClient.invalidateQueries({
    queryKey: loyaltyKeys.points.ledgersByAccount(loyaltyAccountId),
  });
  void queryClient.invalidateQueries({
    predicate: (q) =>
      Array.isArray(q.queryKey) &&
      q.queryKey[0] === "loyalty" &&
      q.queryKey[1] === "eventable" &&
      q.queryKey[3] === loyaltyAccountId,
  });
}
