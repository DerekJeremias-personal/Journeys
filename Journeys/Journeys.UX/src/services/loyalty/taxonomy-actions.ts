"use server";

import type { ApiResponse } from "@/lib/api-types";
import type { TaxonomyDto } from "@/lib/campaign-types";

function emptyOk<T>(data: T): ApiResponse<T> {
  return { success: true, data, timestamp: new Date().toISOString() };
}

export async function getTaxonomiesByType(_params: {
  tenantId: string;
  taxonomyType: string;
  taxonomyCategory?: string | null;
  pageSize?: number;
  continuationToken?: string | null;
}): Promise<ApiResponse<TaxonomyDto[]>> {
  return emptyOk([]);
}

export async function browseTaxonomy(_params: {
  tenantId: string;
  taxonomyType: string;
  taxonomyCategory?: string | null;
  pageSize?: number;
  continuationToken?: string | null;
}): Promise<ApiResponse<TaxonomyDto[]>> {
  return emptyOk([]);
}

export async function getTaxonomyCategories(_catalogId: string): Promise<ApiResponse<string[]>> {
  return emptyOk([]);
}
