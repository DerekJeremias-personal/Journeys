import type { CampaignListItem, SchemaListItem } from "@/lib/api-types";
import { LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";

function readString(row: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return undefined;
}

export function extractConversationIds(data: unknown): string[] {
  const ids: string[] = [];
  for (const item of extractEntities(data)) {
    if (!item || typeof item !== "object") continue;
    const id = readString(item as Record<string, unknown>, "conversationId", "ConversationId");
    if (id) ids.push(id);
  }
  return ids;
}

function readRecord(row: Record<string, unknown>, ...keys: string[]): Record<string, unknown> | undefined {
  for (const key of keys) {
    const value = row[key];
    if (value && typeof value === "object" && !Array.isArray(value)) return value as Record<string, unknown>;
  }
  return undefined;
}

export function normalizeCampaignRow(row: unknown): CampaignListItem | null {
  if (!row || typeof row !== "object") return null;
  const rec = row as Record<string, unknown>;
  return {
    id: readString(rec, "id", "Id", "ID"),
    name: readString(rec, "name", "Name"),
    status: readString(rec, "status", "Status"),
    extCampaignId: readString(rec, "extCampaignId", "ExtCampaignId"),
    startDate: readString(rec, "startDate", "StartDate"),
    endDate: readString(rec, "endDate", "EndDate"),
    // Account tier/progress reads the journey tree off the campaign list.
    journey: readRecord(rec, "journey", "Journey")
  };
}

export function extractEntities(data: unknown): unknown[] {
  if (Array.isArray(data)) return data;
  if (data && typeof data === "object") {
    const obj = data as Record<string, unknown>;
    for (const key of ["entities", "Entities", "items", "Items"]) {
      const value = obj[key];
      if (Array.isArray(value)) return value;
    }
  }
  return [];
}

export function readModelMetaData(row: Record<string, unknown>): Record<string, string> | undefined {
  const raw = row.modelMetaData ?? row.ModelMetaData;
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) return undefined;
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === "string" && value.trim()) out[key] = value.trim();
  }
  return Object.keys(out).length > 0 ? out : undefined;
}

export function normalizeSchema(row: unknown): SchemaListItem | null {
  if (!row || typeof row !== "object") return null;
  const rec = row as Record<string, unknown>;
  return {
    id: readString(rec, "id", "ID", "Id"),
    name: readString(rec, "name", "Name"),
    status: readString(rec, "status", "Status"),
    modelType: readString(rec, "modelType", "ModelType"),
    tag: readString(rec, "tag", "Tag"),
    modelMetaData: readModelMetaData(rec),
    attributes: Array.isArray(rec.attributes)
      ? (rec.attributes as SchemaListItem["attributes"])
      : Array.isArray(rec.Attributes)
        ? (rec.Attributes as SchemaListItem["attributes"])
        : undefined
  };
}

export function extractContinuationToken(data: unknown): string | null {
  if (!data || typeof data !== "object" || Array.isArray(data)) return null;
  const rec = data as Record<string, unknown>;
  const token = rec.continuationToken ?? rec.ContinuationToken;
  return typeof token === "string" && token.length > 0 ? token : null;
}

export function attributeNamesFromRows(
  rows: Record<string, unknown>[]
): { name: string }[] {
  if (rows.length === 0) return [{ name: "id" }, { name: "name" }];
  return Object.keys(rows[0]).map((name) => ({ name }));
}

export function pickLiveSchema(schemas: SchemaListItem[], name: string): SchemaListItem | null {
  const target = name.toLowerCase();
  for (const schema of schemas) {
    const type = schema.modelType?.toLowerCase();
    if (type && type !== LOYALTY_MODEL_TYPE) continue;
    if (schema.name?.toLowerCase() === target && schema.status?.toLowerCase() === "live") {
      return schema;
    }
  }
  return null;
}
