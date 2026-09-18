import Link from "next/link";
import { notFound } from "next/navigation";

import { AccountActionsDropdown, LoyaltyAccountDetailClient } from "@/components/loyalty/accounts";
import {
  getLoyaltyAccountByExternalId,
  getLoyaltyAccountById,
  queryData,
} from "@/services/loyalty/actions";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import { LOYALTY_ACCOUNT_IDENTIFIER_QUERY } from "@/services/loyalty/utils/account-identifiers";
import { resolveAccountIdentifiers } from "@/services/loyalty/utils/account-identifier-resolution";
import { isReservedAccountRouteId } from "@/services/loyalty/utils/reserved-account-route-ids";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function LoyaltyAccountDetailPage({ params }: PageProps) {
  const { id } = await params;
  if (isReservedAccountRouteId(id)) {
    notFound();
  }

  const identifiers = await resolveAccountIdentifiers(id, {
    queryIdentifier: (identifier) =>
      queryData<Record<string, unknown>>({
        schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME,
        queryString: LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
        queryArgs: { "@id": identifier },
        pageSize: 1,
      }),
    getById: getLoyaltyAccountById,
    getByExternalId: getLoyaltyAccountByExternalId,
  });
  const accountLabel = identifiers.loyaltyAccountXReference || identifiers.loyaltyAccountId || id;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/accounts">
            ← Accounts
          </Link>
          <h1 className="mt-2 text-2xl font-semibold tracking-tight">Loyalty account</h1>
          <p className="text-sm text-muted-foreground">{accountLabel}</p>
        </div>
        {identifiers.found && (
          <AccountActionsDropdown
            loyaltyAccountId={identifiers.loyaltyAccountId}
            loyaltyAccountXReference={identifiers.loyaltyAccountXReference}
            detailAccountId={id}
          />
        )}
      </div>
      <LoyaltyAccountDetailClient accountId={id} />
    </div>
  );
}
