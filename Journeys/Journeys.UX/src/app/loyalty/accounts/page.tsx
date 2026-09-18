import { Suspense } from "react";

import { LoyaltyAccountsListClient } from "@/components/loyalty/accounts";
import { Skeleton } from "@/components/ui/skeleton";

export default function AccountsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold tracking-tight">Accounts</h1>
      {/* The list reads `account` search params, so it needs its own boundary
          to keep the rest of the route prerenderable. */}
      <Suspense fallback={<Skeleton className="h-64 w-full" />}>
        <LoyaltyAccountsListClient />
      </Suspense>
    </div>
  );
}
