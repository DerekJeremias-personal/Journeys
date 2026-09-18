"use client";

/**
 * PointAccountManageModal — Dialog with Deposit / Spend / Expire tabs.
 *
 * Each panel owns its own form + mutation. The parent supplies the
 * `loyaltyAccountId` + the selected point account so the panels can pre-fill
 * the point account type id and surface the current balance for validation.
 */

import { ArrowDownToLine, ArrowUpFromLine, Hourglass } from "lucide-react";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { AccountPointBalance, PointAccountTypeListItem } from "@/lib/api-types";

import { DepositPanel } from "./point-account-panels/deposit-panel";
import { SpendPanel } from "./point-account-panels/spend-panel";
import { ExpirePanel } from "./point-account-panels/expire-panel";

/**
 * Every write needs a point account type id, so the modal only accepts a
 * balance row that actually carries one.
 */
export type ManageablePointAccount = AccountPointBalance & { pointAccountTypeId: string };

export function isManageablePointAccount(
  account: AccountPointBalance
): account is ManageablePointAccount {
  return typeof account.pointAccountTypeId === "string" && account.pointAccountTypeId.length > 0;
}

export interface PointAccountManageModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  loyaltyAccountId: string;
  pointAccount: ManageablePointAccount;
  /** Optional point account types, used to resolve a friendly account name. */
  pointAccountTypes?: PointAccountTypeListItem[];
  onUpdated?: () => void;
}

function readText(value: unknown): string | null {
  return typeof value === "string" && value.trim().length > 0 ? value : null;
}

export function PointAccountManageModal({
  open,
  onOpenChange,
  loyaltyAccountId,
  pointAccount,
  pointAccountTypes = [],
  onUpdated,
}: PointAccountManageModalProps) {
  const pointAccountTypeId = pointAccount.pointAccountTypeId;
  const match = pointAccountTypes.find((p) => readText(p.id) === pointAccountTypeId);
  const accountName = (match ? readText(match.name) : null) ?? pointAccountTypeId;
  const currentBalance = pointAccount.currentBalance ?? undefined;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Manage points · {accountName}</DialogTitle>
          <DialogDescription>
            Current balance{" "}
            {typeof currentBalance === "number" ? currentBalance.toLocaleString() : "Unavailable"} pts ·
            Lifetime {(pointAccount.lifetimeTotal ?? 0).toLocaleString()} pts
          </DialogDescription>
        </DialogHeader>

        <Tabs defaultValue="deposit">
          <TabsList className="w-full justify-start">
            <TabsTrigger value="deposit">
              <ArrowDownToLine className="mr-1.5 h-4 w-4" />
              Deposit
            </TabsTrigger>
            <TabsTrigger value="spend">
              <ArrowUpFromLine className="mr-1.5 h-4 w-4" />
              Spend
            </TabsTrigger>
            <TabsTrigger value="expire">
              <Hourglass className="mr-1.5 h-4 w-4" />
              Expire
            </TabsTrigger>
          </TabsList>
          <TabsContent value="deposit" className="pt-4">
            <DepositPanel
              loyaltyAccountId={loyaltyAccountId}
              pointAccountTypeId={pointAccountTypeId}
              onUpdated={onUpdated}
            />
          </TabsContent>
          <TabsContent value="spend" className="pt-4">
            <SpendPanel
              loyaltyAccountId={loyaltyAccountId}
              pointAccountTypeId={pointAccountTypeId}
              currentBalance={currentBalance}
              onUpdated={onUpdated}
            />
          </TabsContent>
          <TabsContent value="expire" className="pt-4">
            <ExpirePanel
              loyaltyAccountId={loyaltyAccountId}
              pointAccountTypeId={pointAccountTypeId}
              currentBalance={currentBalance}
              onUpdated={onUpdated}
            />
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
