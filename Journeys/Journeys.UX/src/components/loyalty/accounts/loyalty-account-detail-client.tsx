"use client";

/**
 * LoyaltyAccountDetailClient — composes the account detail screen.
 *
 * Layout:
 *   - Account summary and point balances
 *   - Schema-driven `<DynamicEntityDetails>` for the remaining fields
 *   - `<AccountCampaignProgress>` — campaign journey progress
 *   - `<EventableModelsSection>` — events grouped by Live eventable schemas
 */

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

import { DynamicEntityDetails } from "@/components/loyalty/dynamic-data";
import { PointsAccountsCard } from "@/components/loyalty/points";
import type { AccountPointBalance } from "@/lib/api-types";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import {
  getAccountPointBalances,
  getCampaigns,
  getLoyaltyAccountByExternalId,
  getLoyaltyAccountById,
  getPointAccountTypes,
  getSchemaByName,
  queryData,
} from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import {
  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
  normalizeEntityWithEvent,
  resolveLoyaltyAccountId,
} from "@/services/loyalty/utils/account-identifiers";
import { getSchemaFieldValue } from "@/services/loyalty/utils/grid-columns";

import { AccountCampaignProgress } from "./account-campaign-progress";
import { EventableModelsSection } from "./eventable-models-section";
import { resolveCurrentTierLabel, type JourneyRef } from "./account-tier";

interface AccountDetailEntity {
  id?: string;
  loyaltyAccountId?: string;
  event?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface LoyaltyAccountDetailClientProps {
  accountId: string;
}

function readAccountText(entity: Record<string, unknown>, fields: string[]): string | null {
  for (const field of fields) {
    const value = getSchemaFieldValue(entity, field);
    if (typeof value === "string" && value.trim().length > 0) return value;
    if (typeof value === "number" && Number.isFinite(value)) return value.toString();
  }
  return null;
}

function getStatusLabel(value: unknown): string {
  if (typeof value === "boolean") return value ? "Active" : "Inactive";
  if (typeof value === "string" && value.trim().length > 0) return value;
  return "Unknown";
}

function getExternalAccountIds(account: AccountDetailEntity): string[] {
  const ids = new Set<string>();
  const extAccountId = account["extAccountId"];
  if (typeof extAccountId === "string" && extAccountId.trim().length > 0) ids.add(extAccountId);

  const knownExternalIds = account["knownExternalIds"];
  if (Array.isArray(knownExternalIds)) {
    for (const id of knownExternalIds) {
      if (typeof id === "string" && id.trim().length > 0) ids.add(id);
    }
  }

  return [...ids];
}

async function getAccountDetailEntityByIdentifier(
  identifier: string
): Promise<Record<string, unknown> | null> {
  const result = await queryData<AccountDetailEntity>({
    schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME,
    queryString: LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
    queryArgs: { "@id": identifier },
    pageSize: 1,
  });
  if (!result.success) return null;
  return normalizeEntityWithEvent(result.data?.[0]);
}

async function mergeAccountWithEventPayload(
  account: AccountDetailEntity
): Promise<AccountDetailEntity> {
  for (const externalId of getExternalAccountIds(account)) {
    const eventEntity = await getAccountDetailEntityByIdentifier(externalId);
    if (eventEntity) return { ...account, ...eventEntity };
  }
  return account;
}

function AccountSummaryCard({
  entity,
  loyaltyAccountId,
  pointAccounts,
  currentTierLabel,
}: {
  entity: Record<string, unknown>;
  loyaltyAccountId: string;
  pointAccounts: AccountPointBalance[];
  currentTierLabel: string | null;
}) {
  const firstName = readAccountText(entity, ["firstName", "firstname"]);
  const lastName = readAccountText(entity, ["lastName", "lastname"]);
  const joinedName = [firstName, lastName].filter(Boolean).join(" ");
  const fullName =
    readAccountText(entity, ["fullName", "fullname", "memberName", "name"]) ??
    (joinedName.length > 0 ? joinedName : null);
  const externalId = readAccountText(entity, [
    "sourceRecordId",
    "extAccountId",
    "customerid",
    "customerId",
    "profileid",
    "profileId",
    "eventId",
  ]);
  const memberIdentifier =
    readAccountText(entity, ["loyaltyMemberId", "emailAddress", "email", "mobilePhone", "mobilephone"]) ??
    externalId;
  const status = getStatusLabel(
    getSchemaFieldValue(entity, "status") ??
      getSchemaFieldValue(entity, "accountStatus") ??
      getSchemaFieldValue(entity, "isActive") ??
      getSchemaFieldValue(entity, "active") ??
      getSchemaFieldValue(entity, "isloyaltymember") ??
      getSchemaFieldValue(entity, "isLoyaltyMember")
  );
  const currentTier =
    currentTierLabel ?? readAccountText(entity, ["currentTier", "tier", "tierName", "campaignName"]);
  const totalBalance = pointAccounts.reduce((sum, account) => sum + (account.currentBalance ?? 0), 0);

  const fields = [
    { label: "External ID", value: externalId },
    { label: "Member identifier", value: memberIdentifier },
    { label: "Current tier", value: currentTier },
    { label: "Points balance", value: pointAccounts.length > 0 ? totalBalance.toLocaleString() : null },
    { label: "Status", value: status },
    { label: "Account ID", value: loyaltyAccountId },
  ];

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4">
        <div>
          <CardTitle className="text-base">Account profile</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{fullName ?? externalId ?? loyaltyAccountId}</p>
        </div>
        <Badge variant="outline">{status}</Badge>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-3 text-sm sm:grid-cols-2">
          {fields.map((field) => (
            <div key={field.label} className="min-w-0 rounded-md border bg-muted/20 p-3">
              <dt className="text-xs font-medium uppercase text-muted-foreground">{field.label}</dt>
              <dd className="mt-1 break-words font-medium">{field.value ?? "—"}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  );
}

export function LoyaltyAccountDetailClient({ accountId }: LoyaltyAccountDetailClientProps) {
  const schemaQuery = useQuery({
    queryKey: loyaltyKeys.schemas.byName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME),
    queryFn: async () => {
      const r = await getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
      if (!r.success) throw new Error(r.error ?? "Failed to load schema");
      return r.data ?? null;
    },
    staleTime: 5 * 60 * 1000,
  });

  const entityQuery = useQuery({
    queryKey: loyaltyKeys.accounts.detail(accountId),
    queryFn: async () => {
      const entity = await getAccountDetailEntityByIdentifier(accountId);
      if (entity) return entity;

      const byInternal = await getLoyaltyAccountById(accountId);
      if (byInternal.success && byInternal.data) {
        return mergeAccountWithEventPayload(byInternal.data as AccountDetailEntity);
      }

      const byExternal = await getLoyaltyAccountByExternalId(accountId);
      if (byExternal.success && byExternal.data) {
        const account = byExternal.data as AccountDetailEntity;
        const eventEntity = await getAccountDetailEntityByIdentifier(accountId);
        return eventEntity ? { ...account, ...eventEntity } : account;
      }

      return null;
    },
  });

  // Points / journey / ledger endpoints partition by the loyalty account id.
  // The URL may be either the schema document id or the loyalty account id, so
  // resolve it from the loaded entity and fall back to the route param.
  const entityRecord = (entityQuery.data ?? null) as Record<string, unknown> | null;
  const loyaltyAccountId = resolveLoyaltyAccountId(entityRecord, accountId);

  const balancesQuery = useQuery({
    queryKey: loyaltyKeys.points.balancesByAccount(loyaltyAccountId),
    queryFn: async () => {
      const r = await getAccountPointBalances(loyaltyAccountId);
      if (!r.success) throw new Error(r.error ?? "Failed to load balances");
      return r.data ?? [];
    },
    enabled: Boolean(entityQuery.data),
    refetchOnWindowFocus: true,
  });

  const patsQuery = useQuery({
    queryKey: loyaltyKeys.pointAccountTypes.all,
    queryFn: async () => {
      const r = await getPointAccountTypes();
      if (!r.success) throw new Error(r.error ?? "Failed to load point account types");
      return r.data ?? [];
    },
    staleTime: 5 * 60 * 1000,
  });

  const pointsAccounts: AccountPointBalance[] = useMemo(() => balancesQuery.data ?? [], [balancesQuery.data]);
  const pointAccountTypes = patsQuery.data ?? [];

  const accountJourneyQuery = useQuery({
    queryKey: loyaltyKeys.accounts.loyaltyDetail(loyaltyAccountId),
    queryFn: async () => {
      const r = await getLoyaltyAccountById(loyaltyAccountId);
      if (!r.success) throw new Error(r.error ?? "Failed to load account journeys");
      return r.data ?? null;
    },
    enabled: Boolean(entityQuery.data),
  });

  const campaignsQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.list({}),
    queryFn: async () => {
      const r = await getCampaigns();
      if (!r.success) throw new Error(r.error ?? "Failed to load campaigns");
      return r.data ?? [];
    },
    enabled: Boolean(entityQuery.data),
  });

  const currentTierLabel = useMemo(() => {
    const accountJourneys =
      (accountJourneyQuery.data as { journeys?: JourneyRef[] } | null)?.journeys ?? [];
    return resolveCurrentTierLabel(campaignsQuery.data ?? [], accountJourneys);
  }, [accountJourneyQuery.data, campaignsQuery.data]);

  // `SchemaListItem` carries the fields the detail view reads; widen it to the
  // schema shape the schema-driven components expect.
  const schema = useMemo<LoyaltySchema | null>(
    () => (schemaQuery.data ? { ...schemaQuery.data } : null),
    [schemaQuery.data]
  );

  if (schemaQuery.isLoading || entityQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (schemaQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Failed to load schema</AlertTitle>
        <AlertDescription>{(schemaQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  if (entityQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Failed to load account</AlertTitle>
        <AlertDescription>{(entityQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  if (!schema) {
    return <p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>;
  }

  if (!entityQuery.data) {
    return (
      <Alert>
        <AlertTitle>Account not found</AlertTitle>
        <AlertDescription>No account exists with ID {accountId}.</AlertDescription>
      </Alert>
    );
  }

  const entity = entityQuery.data as Record<string, unknown>;

  const balancesError = balancesQuery.error as Error | null;
  const patsError = patsQuery.error as Error | null;

  return (
    <div className="space-y-6">
      {(balancesError || patsError) && (
        <Alert variant="destructive">
          <AlertTitle>Couldn&apos;t load point account data</AlertTitle>
          <AlertDescription>{balancesError?.message ?? patsError?.message ?? "Unknown error."}</AlertDescription>
        </Alert>
      )}
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_400px]">
        <AccountSummaryCard
          entity={entity}
          loyaltyAccountId={loyaltyAccountId}
          pointAccounts={pointsAccounts}
          currentTierLabel={currentTierLabel}
        />
        <PointsAccountsCard
          pointAccounts={pointsAccounts}
          loyaltyAccountId={loyaltyAccountId}
          pointAccountTypes={pointAccountTypes}
          isLoading={balancesQuery.isLoading || patsQuery.isLoading}
        />
      </div>
      <DynamicEntityDetails
        schema={schema}
        entity={entity}
        showTitle={false}
        footerSlot={
          <>
            <AccountCampaignProgress loyaltyAccountId={loyaltyAccountId} />
            <EventableModelsSection loyaltyAccountId={loyaltyAccountId} />
          </>
        }
      />
    </div>
  );
}
