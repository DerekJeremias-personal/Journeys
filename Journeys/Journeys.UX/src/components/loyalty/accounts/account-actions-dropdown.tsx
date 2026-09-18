"use client";

/**
 * AccountActionsDropdown — per-account operator actions on the detail header:
 * assign journey, remove journey, manage tier. The modals own their forms; this
 * component owns the refetch after a successful write.
 */

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ChevronDown, LogIn, LogOut, MoveUp } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import { AccountJourneyModal } from "./account-journey-modal";
import { ManageTierModal } from "./manage-tier-modal";

export interface AccountActionsDropdownProps {
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  /** Route param the detail screen keyed its entity query with, when it differs. */
  detailAccountId?: string;
  onSuccess?: () => void;
}

export function AccountActionsDropdown({
  loyaltyAccountId,
  loyaltyAccountXReference,
  detailAccountId,
  onSuccess,
}: AccountActionsDropdownProps) {
  const queryClient = useQueryClient();
  const [journeyModal, setJourneyModal] = useState<{ open: boolean; mode: "assign" | "remove" }>({
    open: false,
    mode: "assign",
  });
  const [tierModalOpen, setTierModalOpen] = useState(false);
  const accountDetailId = detailAccountId ?? loyaltyAccountId;

  /**
   * A journey or tier move changes the account journeys, the tier label, the
   * point balances and ledger, and any eventable rows keyed to the account.
   */
  const handleSuccess = () => {
    void queryClient.invalidateQueries({ queryKey: loyaltyKeys.accounts.detail(accountDetailId) });
    void queryClient.invalidateQueries({ queryKey: loyaltyKeys.accounts.detail(loyaltyAccountId) });
    void queryClient.invalidateQueries({
      queryKey: loyaltyKeys.accounts.detailByExt(loyaltyAccountXReference),
    });
    void queryClient.invalidateQueries({
      queryKey: loyaltyKeys.accounts.loyaltyDetail(loyaltyAccountId),
    });
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
    onSuccess?.();
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="default" size="sm">
            Actions
            <ChevronDown className="ml-2 h-4 w-4" aria-hidden="true" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuLabel>Account actions</DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={() => setJourneyModal({ open: true, mode: "assign" })}>
            <LogIn className="mr-2 h-4 w-4" aria-hidden="true" />
            Assign journey
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => setJourneyModal({ open: true, mode: "remove" })}>
            <LogOut className="mr-2 h-4 w-4" aria-hidden="true" />
            Remove journey
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => setTierModalOpen(true)}>
            <MoveUp className="mr-2 h-4 w-4" aria-hidden="true" />
            Manage tier
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AccountJourneyModal
        open={journeyModal.open}
        onOpenChange={(open) => setJourneyModal((s) => ({ ...s, open }))}
        mode={journeyModal.mode}
        loyaltyAccountId={loyaltyAccountId}
        loyaltyAccountXReference={loyaltyAccountXReference}
        onSuccess={handleSuccess}
      />
      <ManageTierModal
        open={tierModalOpen}
        onOpenChange={setTierModalOpen}
        loyaltyAccountId={loyaltyAccountId}
        loyaltyAccountXReference={loyaltyAccountXReference}
        onSuccess={handleSuccess}
      />
    </>
  );
}
