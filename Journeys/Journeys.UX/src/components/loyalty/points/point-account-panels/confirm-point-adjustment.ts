"use client";

interface ConfirmPointAdjustmentOptions {
  action: string;
  amount: string;
}

export function confirmPointAdjustment({ action, amount }: ConfirmPointAdjustmentOptions): boolean {
  return window.confirm(`${action} ${amount}? This will update the member balance.`);
}
