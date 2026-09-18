/**
 * Expire amounts are entered either as an absolute point count or as a
 * percentage of the point account's current balance. The API only accepts an
 * absolute amount, so the percent case is resolved before the POST.
 */

export type ResolveExpireAmountInput = {
  amount: number;
  isPercent?: boolean;
  currentBalance?: number;
};

export function resolveExpireAmount({
  amount,
  isPercent,
  currentBalance
}: ResolveExpireAmountInput): number {
  if (!isPercent) return amount;
  if (typeof currentBalance !== "number" || !Number.isFinite(currentBalance)) {
    throw new Error("A currentBalance is required to expire a percentage of the balance");
  }
  return (amount / 100) * currentBalance;
}
