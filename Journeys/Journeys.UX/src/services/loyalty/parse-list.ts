import type { SchemaListItem } from "@/lib/api-types";
import { LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";

function readString(row: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = row[key];
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return undefined;
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

export function normalizeSchema(row: unknown): SchemaListItem | null {
  if (!row || typeof row !== "object") return null;
  const rec = row as Record<string, unknown>;
  return {
    id: readString(rec, "id", "ID", "Id"),
    name: readString(rec, "name", "Name"),
    status: readString(rec, "status", "Status"),
    modelType: readString(rec, "modelType", "ModelType"),
    attributes: Array.isArray(rec.attributes)
      ? (rec.attributes as SchemaListItem["attributes"])
      : Array.isArray(rec.Attributes)
        ? (rec.Attributes as SchemaListItem["attributes"])
        : undefined
  };
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
