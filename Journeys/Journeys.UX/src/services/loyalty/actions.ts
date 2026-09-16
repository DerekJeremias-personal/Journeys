"use server";

import type { ApiResponse, CampaignListItem, SchemaListItem } from "@/lib/api-types";
import { journeysFetch } from "@/lib/journeys-fetch";
import { getManyModelsListBody, isUsableModelId, LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";
import { extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";

const TENANT_SLUG = "session";

export async function getCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getall`, {
    method: "POST",
    body: { pageSize: 100, continuationToken: null }
  });
  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
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

export async function queryData(schemaName: string): Promise<ApiResponse<Record<string, unknown>[]>> {
  if (!isUsableModelId(schemaName)) {
    return {
      success: false,
      error: "A real model name is required; unknown model ids are not sent.",
      timestamp: new Date().toISOString()
    };
  }
  const res = await journeysFetch<unknown>(`events/${TENANT_SLUG}/${schemaName}/admin/query`, {
    method: "POST",
    body: {
      query: "",
      parameters: {},
      pageSize: 50,
      continuationToken: null,
      sortBy: "",
      sortOrder: "ASC"
    }
  });
  if (!res.success) return res as ApiResponse<Record<string, unknown>[]>;
  return { ...res, data: extractEntities(res.data) as Record<string, unknown>[] };
}
