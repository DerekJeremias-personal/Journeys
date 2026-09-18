"use server";

import type {
  AccountPointBalance,
  AgentConversationListItem,
  ApiResponse,
  CampaignListItem,
  PointAccountTypeListItem,
  QueryDataParams,
  SchemaListItem
} from "@/lib/api-types";
import type { Campaign } from "@/lib/campaign-types";
import { getJourneysSession, journeysFetch } from "@/lib/journeys-fetch";
import { getManyModelsListBody, isUsableModelId, LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";
import { mergeCampaignLists } from "@/lib/campaign-authoring-list";
import { resolveExpireAmount } from "./point-expire-amount";
import {
  extractContinuationToken,
  extractConversationIds,
  extractEntities,
  normalizeCampaignRow,
  normalizeSchema,
  pickLiveSchema
} from "./parse-list";

const TENANT_SLUG = "session";

function asCampaignRows(data: unknown): CampaignListItem[] {
  return extractEntities(data)
    .map(normalizeCampaignRow)
    .filter((row): row is CampaignListItem => row !== null);
}

async function getCampaignsByStatus(
  status: string
): Promise<ApiResponse<CampaignListItem[]>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/query`, {
    method: "POST",
    body: {
      query: "c.status = @status",
      parameters: { "@status": status },
      pageSize: 100,
      continuationToken: null
    }
  });
  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
  return { ...res, data: asCampaignRows(res.data) };
}

export async function getCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
  const getAll = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getall`, {
    method: "POST",
    body: { pageSize: 100, continuationToken: null }
  });
  const getAllPage: ApiResponse<CampaignListItem[]> = getAll.success
    ? { ...getAll, data: asCampaignRows(getAll.data) }
    : (getAll as ApiResponse<CampaignListItem[]>);

  const pages = await Promise.all([
    Promise.resolve(getAllPage),
    getCampaignsByStatus("draft"),
    getCampaignsByStatus("pause")
  ]);
  const succeeded = pages.filter((page) => page.success);
  if (succeeded.length === 0) {
    return pages[0] ?? {
      success: false,
      error: "Failed to load campaigns",
      timestamp: new Date().toISOString()
    };
  }
  return {
    success: true,
    data: mergeCampaignLists(succeeded.map((page) => page.data ?? [])),
    timestamp: new Date().toISOString()
  };
}

export async function getCampaignsByFilters(
  filters: Record<string, unknown>
): Promise<ApiResponse<CampaignListItem[]>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/query`, {
    method: "POST",
    body: filters
  });
  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
  return { ...res, data: asCampaignRows(res.data) };
}

export async function getCampaign(
  id: string,
  status?: string
): Promise<ApiResponse<CampaignListItem>> {
  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}`, {
    searchParams: { campaignStatus: status }
  });
}

export async function updateCampaign(
  data: Campaign | CampaignListItem
): Promise<ApiResponse<CampaignListItem>> {
  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/save`, {
    method: "POST",
    body: data
  });
}

export async function deleteCampaign(
  id: string,
  status: string
): Promise<ApiResponse<void>> {
  return journeysFetch<void>(`campaigns/${TENANT_SLUG}/${id}`, {
    method: "DELETE",
    searchParams: { status }
  });
}

export async function copyCampaign(
  id: string,
  status: string,
  name?: string
): Promise<ApiResponse<CampaignListItem>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/${id}/copy`, {
    method: "POST",
    searchParams: { status },
    body: name ? { name } : {}
  });
  if (!res.success) return res as ApiResponse<CampaignListItem>;
  const copied = normalizeCampaignRow(res.data);
  return { ...res, data: copied ?? undefined };
}

export async function restoreCampaign(id: string): Promise<ApiResponse<CampaignListItem>> {
  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}/restore`, {
    method: "POST",
    searchParams: { status: "archive" }
  });
}

export async function validateCampaign(
  data: CampaignListItem
): Promise<ApiResponse<unknown>> {
  return journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/validate`, {
    method: "POST",
    body: data
  });
}

export async function getCampaignVersions(
  ext: string
): Promise<ApiResponse<CampaignListItem[]>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/versions/${ext}`);
  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
}

export async function getArchivedCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/archived`);
  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
}

export async function getLiveByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/live/${ext}`);
}

export async function getDraftByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/draft/${ext}`);
}

export async function getPointAccountTypes(): Promise<ApiResponse<PointAccountTypeListItem[]>> {
  const res = await journeysFetch<unknown>(
    `campaigns/${TENANT_SLUG}/pointaccounttype/getall`,
    {
      method: "POST",
      body: { pageSize: 100, continuationToken: null }
    }
  );
  if (!res.success) return res as ApiResponse<PointAccountTypeListItem[]>;
  return { ...res, data: extractEntities(res.data) as PointAccountTypeListItem[] };
}

export async function listAgentConversations(): Promise<
  ApiResponse<AgentConversationListItem[]>
> {
  const res = await journeysFetch<unknown>("campaign-agent/conversations");
  if (!res.success) return res as ApiResponse<AgentConversationListItem[]>;
  const items = extractConversationIds(res.data).map((conversationId) => ({ conversationId }));
  return { ...res, data: items };
}

export async function getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>> {
  const res = await journeysFetch<unknown>(`schemas/${TENANT_SLUG}/model/all`, {
    method: "POST",
    body: getManyModelsListBody()
  });
  if (!res.success) return res as ApiResponse<SchemaListItem[]>;
  const schemas = extractEntities(res.data)
    .map(normalizeSchema)
    .filter((s): s is SchemaListItem => s !== null)
    .filter((s) => !s.modelType || s.modelType.toLowerCase() === LOYALTY_MODEL_TYPE);
  return { ...res, data: schemas };
}

export async function getSchemaByName(
  name: string
): Promise<ApiResponse<SchemaListItem | null>> {
  const res = await getAllSchemas();
  if (!res.success) return res as ApiResponse<SchemaListItem | null>;
  return { ...res, data: pickLiveSchema(res.data ?? [], name) };
}

/**
 * Account reads. These are GETs, so they never carry the `X-Journeys-Audit`
 * header — only account/journey writes do.
 */
export async function getLoyaltyAccountById(
  id: string
): Promise<ApiResponse<Record<string, unknown>>> {
  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/${id}`);
}

export async function getLoyaltyAccountByExternalId(
  extId: string
): Promise<ApiResponse<Record<string, unknown>>> {
  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/ext/${extId}`);
}

export async function getAccountPointBalances(
  loyaltyAccountId: string
): Promise<ApiResponse<AccountPointBalance[]>> {
  const res = await journeysFetch<unknown>(
    `accounts/${TENANT_SLUG}/points/balances/${loyaltyAccountId}`
  );
  if (!res.success) return res as ApiResponse<AccountPointBalance[]>;
  return { ...res, data: extractEntities(res.data) as AccountPointBalance[] };
}

/** Point ledger rows for the account; campaign progress reads balances from these. */
export async function getAccountPoints(
  loyaltyAccountId: string
): Promise<ApiResponse<unknown[]>> {
  const res = await journeysFetch<unknown>(`accounts/${TENANT_SLUG}/points/${loyaltyAccountId}`);
  if (!res.success) return res as ApiResponse<unknown[]>;
  return { ...res, data: extractEntities(res.data) };
}

/**
 * Point writes. Unlike the reads above these carry `X-Journeys-Audit`, which
 * `journeysFetch` builds from the `audit` option plus the session user id.
 */

export type PointMutationInput = {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  amount: number;
  comment: string;
};

export type ExpirePointsInput = PointMutationInput & {
  isPercent?: boolean;
  currentBalance?: number;
};

const POINT_ADJUSTMENT = "Point Adjustment";

async function requireUserId(): Promise<string | null> {
  const s = await getJourneysSession();
  return s?.userId ?? null;
}

function missingUserId<T>(): ApiResponse<T> {
  return {
    success: false,
    error: "Session user id is required",
    timestamp: new Date().toISOString()
  };
}

export async function depositPoints(
  input: PointMutationInput
): Promise<ApiResponse<unknown>> {
  const userId = await requireUserId();
  if (!userId) return missingUserId();

  return journeysFetch<unknown>(`accounts/${TENANT_SLUG}/points/deposit`, {
    method: "POST",
    body: {
      loyaltyAccountId: input.loyaltyAccountId,
      pointAccountTypeId: input.pointAccountTypeId,
      amount: input.amount,
      eventId: crypto.randomUUID(),
      eventType: "admin",
      depositDate: new Date().toISOString(),
      userId
    },
    audit: {
      loyaltyMemberId: input.loyaltyAccountId,
      actionType: POINT_ADJUSTMENT,
      action: "Deposit",
      comment: input.comment
    }
  });
}

async function postWithdrawal(
  input: PointMutationInput,
  amount: number,
  action: "Spend" | "Expire"
): Promise<ApiResponse<unknown>> {
  const userId = await requireUserId();
  if (!userId) return missingUserId();

  return journeysFetch<unknown>(`accounts/${TENANT_SLUG}/points/withdrawal`, {
    method: "POST",
    body: {
      loyaltyAccountId: input.loyaltyAccountId,
      pointAccountTypeId: input.pointAccountTypeId,
      amount,
      eventId: crypto.randomUUID(),
      eventType: "admin",
      withdrawalDate: new Date().toISOString(),
      status: "shipped",
      userId
    },
    audit: {
      loyaltyMemberId: input.loyaltyAccountId,
      actionType: POINT_ADJUSTMENT,
      action,
      comment: input.comment
    }
  });
}

export async function withdrawPoints(
  input: PointMutationInput
): Promise<ApiResponse<unknown>> {
  return postWithdrawal(input, input.amount, "Spend");
}

/**
 * Expire reuses the withdrawal route; the audit `action` is what distinguishes
 * it. There is no `points/expire` path.
 */
export async function expirePoints(
  input: ExpirePointsInput
): Promise<ApiResponse<unknown>> {
  let amount: number;
  try {
    amount = resolveExpireAmount({
      amount: input.amount,
      isPercent: input.isPercent,
      currentBalance: input.currentBalance
    });
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
      timestamp: new Date().toISOString()
    };
  }
  return postWithdrawal(input, amount, "Expire");
}

/**
 * Journey movement writes. Enter/exit are GETs on the API, but they still
 * mutate the account, so they carry `X-Journeys-Audit` like the point writes.
 */

const JOURNEY_MOVEMENT = "Journey Movement";

export type JourneyMovementInput = {
  campaignId: string;
  journeyId: string;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  comment: string;
};

async function moveAccountJourney(
  input: JourneyMovementInput,
  segment: "ManuallyEnter" | "ManuallyExit",
  action: "Add" | "Remove"
): Promise<ApiResponse<unknown>> {
  return journeysFetch<unknown>(
    `journey/${TENANT_SLUG}/${segment}/${input.campaignId}/Journey/${input.journeyId}` +
      `/ForAccount/${input.loyaltyAccountXReference}`,
    {
      method: "GET",
      audit: {
        loyaltyMemberId: input.loyaltyAccountId,
        actionType: JOURNEY_MOVEMENT,
        action,
        comment: input.comment
      }
    }
  );
}

export async function enterJourney(
  input: JourneyMovementInput
): Promise<ApiResponse<unknown>> {
  return moveAccountJourney(input, "ManuallyEnter", "Add");
}

export async function exitJourney(
  input: JourneyMovementInput
): Promise<ApiResponse<unknown>> {
  return moveAccountJourney(input, "ManuallyExit", "Remove");
}

export type TierMoveInput = {
  loyaltyAccountId: string;
  loyaltyAccountXReference?: string;
  targetCampaignId: string;
  targetJourneyId: string;
  comment: string;
};

/**
 * `TierMovePreviewResponse` fields the modal reads. They are optional here
 * because the UX does not validate the API payload; the modal formats missing
 * numbers rather than asserting them.
 */
export type TierMovePreview = {
  tierQualificationAmount?: number;
  dealerSpendableAmount?: number;
  currentTierQualificationBalance?: number;
  currentDealerSpendableBalance?: number;
  projectedTierQualificationBalance?: number;
  projectedDealerSpendableBalance?: number;
  isTierDemotion?: boolean;
  isSameJourney?: boolean;
  hasNegativeAdjustment?: boolean;
  validationErrors?: string[];
};

/** Preview is a read: no audit header. */
export async function previewTierMove(
  input: TierMoveInput
): Promise<ApiResponse<TierMovePreview>> {
  const adminUserId = await requireUserId();
  if (!adminUserId) return missingUserId();

  return journeysFetch<TierMovePreview>(`journey/${TENANT_SLUG}/PreviewTierMove`, {
    method: "POST",
    body: {
      loyaltyAccountId: input.loyaltyAccountId,
      loyaltyAccountXReference: input.loyaltyAccountXReference,
      targetCampaignId: input.targetCampaignId,
      targetJourneyId: input.targetJourneyId,
      comment: input.comment,
      adminUserId
    }
  });
}

/**
 * The tier move is a single `MoveTier` call. When the API rejects it the error
 * is surfaced as-is; there is no journey enter/exit substitute.
 */
export async function moveTier(input: TierMoveInput): Promise<ApiResponse<unknown>> {
  const adminUserId = await requireUserId();
  if (!adminUserId) return missingUserId();

  return journeysFetch<unknown>(`journey/${TENANT_SLUG}/MoveTier`, {
    method: "POST",
    body: {
      loyaltyAccountId: input.loyaltyAccountId,
      loyaltyAccountXReference: input.loyaltyAccountXReference,
      targetCampaignId: input.targetCampaignId,
      targetJourneyId: input.targetJourneyId,
      comment: input.comment,
      adminUserId
    },
    audit: {
      loyaltyMemberId: input.loyaltyAccountId,
      actionType: JOURNEY_MOVEMENT,
      action: "Move Tier",
      comment: input.comment
    }
  });
}

export async function queryData<T = Record<string, unknown>>(
  params: QueryDataParams
): Promise<ApiResponse<T[]>> {
  if (!isUsableModelId(params.schemaName)) {
    return {
      success: false,
      error: "A real model name is required; unknown model ids are not sent.",
      timestamp: new Date().toISOString()
    };
  }
  const pageSize = params.pageSize ?? 50;
  const res = await journeysFetch<unknown>(
    `events/${TENANT_SLUG}/${params.schemaName}/admin/query`,
    {
      method: "POST",
      body: {
        query: params.queryString ?? "",
        parameters: params.queryArgs ?? {},
        pageSize,
        continuationToken: params.continuationToken ?? null,
        sortBy: params.sortBy ?? "",
        sortOrder: params.sortOrder ?? "ASC",
        loyaltyAccountId: params.loyaltyAccountId
      }
    }
  );
  if (!res.success) return res as ApiResponse<T[]>;
  return {
    ...res,
    data: extractEntities(res.data) as T[],
    meta: {
      continuationToken: extractContinuationToken(res.data),
      pageSize
    }
  };
}
