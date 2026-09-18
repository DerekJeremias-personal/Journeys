import {
  normalizeEntityWithEvent,
  resolveLoyaltyAccountId,
  resolveLoyaltyAccountXReference,
} from "./account-identifiers";

type AccountRecord = Record<string, unknown>;

type LookupResult<T> = {
  success: boolean;
  data?: T | null;
  error?: string;
};

type AccountIdentifierResolutionDependencies = {
  queryIdentifier: (id: string) => Promise<LookupResult<AccountRecord[]>>;
  getById: (id: string) => Promise<LookupResult<AccountRecord>>;
  getByExternalId: (id: string) => Promise<LookupResult<AccountRecord>>;
};

export type AccountIdentifierResolution = {
  found: boolean;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
};

function readString(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function readFirstString(value: unknown): string | undefined {
  return Array.isArray(value) ? value.map(readString).find(Boolean) : undefined;
}

function resolutionFromAccount(account: AccountRecord, fallbackId: string): AccountIdentifierResolution {
  return {
    found: true,
    loyaltyAccountId: readString(account.id) ?? fallbackId,
    loyaltyAccountXReference:
      readString(account.extAccountId) ?? readFirstString(account.knownExternalIds) ?? fallbackId,
  };
}

export async function resolveAccountIdentifiers(
  id: string,
  dependencies: AccountIdentifierResolutionDependencies
): Promise<AccountIdentifierResolution> {
  const queryResult = await dependencies.queryIdentifier(id);
  const queryEntity =
    queryResult.success && queryResult.data?.[0]
      ? normalizeEntityWithEvent(queryResult.data[0]) ?? queryResult.data[0]
      : null;

  if (queryEntity) {
    const loyaltyAccountId = resolveLoyaltyAccountId(queryEntity, id);
    return {
      found: true,
      loyaltyAccountId,
      loyaltyAccountXReference: resolveLoyaltyAccountXReference(queryEntity, loyaltyAccountId),
    };
  }

  const byInternal = await dependencies.getById(id);
  if (byInternal.success && byInternal.data) {
    return resolutionFromAccount(byInternal.data, id);
  }

  const byExternal = await dependencies.getByExternalId(id);
  if (byExternal.success && byExternal.data) {
    return resolutionFromAccount(byExternal.data, id);
  }

  return {
    found: false,
    loyaltyAccountId: id,
    loyaltyAccountXReference: id,
  };
}
