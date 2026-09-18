"use client";

/**
 * PointsAccountsCard — list of point accounts for a loyalty account with a
 * Manage button per row that opens `PointAccountManageModal`.
 */

import { useState } from "react";
import { Settings2, Wallet } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { AccountPointBalance, PointAccountTypeListItem } from "@/lib/api-types";

import {
  isManageablePointAccount,
  PointAccountManageModal,
  type ManageablePointAccount,
} from "./point-account-manage-modal";

export interface PointsAccountsCardProps {
  pointAccounts: AccountPointBalance[];
  loyaltyAccountId: string;
  pointAccountTypes?: PointAccountTypeListItem[];
  isLoading?: boolean;
  onUpdated?: () => void;
}

function getPointAccountKey(account: AccountPointBalance, index: number): string {
  return `${account.accountId}:${account.pointAccountTypeId}:${index}`;
}

function readText(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

export function PointsAccountsCard({
  pointAccounts,
  loyaltyAccountId,
  pointAccountTypes = [],
  isLoading = false,
  onUpdated,
}: PointsAccountsCardProps) {
  const [selected, setSelected] = useState<ManageablePointAccount | null>(null);

  const renderAccountName = (account: AccountPointBalance): string => {
    const match = pointAccountTypes.find((p) => readText(p.id) === account.pointAccountTypeId);
    return (match ? readText(match.name) : null) ?? account.pointAccountTypeId ?? "Point account";
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Wallet className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
          Point accounts
        </CardTitle>
        <CardDescription>Balances by point account type</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        ) : pointAccounts.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">No point accounts found.</p>
        ) : (
          <ul className="space-y-3">
            {pointAccounts.map((account, index) => (
              <li
                key={getPointAccountKey(account, index)}
                className="flex items-center justify-between gap-3 rounded-lg border p-3"
              >
                <div className="min-w-0">
                  <p className="text-sm font-medium truncate">{renderAccountName(account)}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                    <span>
                      Balance{" "}
                      <span className="text-foreground font-semibold tabular-nums">
                        {(account.currentBalance ?? 0).toLocaleString()}
                      </span>
                    </span>
                    <span aria-hidden="true">•</span>
                    <span>
                      Lifetime{" "}
                      <span className="text-foreground font-semibold tabular-nums">
                        {(account.lifetimeTotal ?? 0).toLocaleString()}
                      </span>
                    </span>
                  </div>
                </div>
                {isManageablePointAccount(account) && (
                  <Button size="sm" variant="outline" onClick={() => setSelected(account)}>
                    <Settings2 className="mr-1.5 h-4 w-4" aria-hidden="true" />
                    Manage
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      {selected && (
        <PointAccountManageModal
          open={selected !== null}
          onOpenChange={(open) => !open && setSelected(null)}
          loyaltyAccountId={loyaltyAccountId}
          pointAccount={selected}
          pointAccountTypes={pointAccountTypes}
          onUpdated={() => {
            setSelected(null);
            onUpdated?.();
          }}
        />
      )}
    </Card>
  );
}
