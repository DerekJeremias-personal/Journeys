/**
 * Human-readable labels for schema-driven surfaces.
 *
 * Schema and attribute names arrive as raw symbols (`orderId`,
 * `customer_profile`), and entity rows key their human identifier under one of
 * several historical field spellings. These helpers turn both into text an
 * operator can read.
 */

import type { LoyaltySchema } from "@/lib/loyalty-schema-types";

import { generateColumnsFromSchema, getSchemaFieldValue } from "./grid-columns";

type LabelSchema = Pick<LoyaltySchema, "name" | "displayName" | "attributes">;

const KNOWN_INITIALISMS = new Map([
  ["id", "ID"],
  ["url", "URL"],
  ["api", "API"],
  ["ai", "AI"],
  ["sdk", "SDK"],
]);

const ENTITY_LABEL_FIELDS = [
  "orderId",
  "orderid",
  "eventType",
  "eventName",
  "externalId",
  "extAccountId",
  "sourceRecordId",
  "loyaltyAccountId",
  "profileId",
  "profileid",
  "customerId",
  "customerid",
  "name",
  "displayName",
  "title",
] as const;

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function formatWord(word: string): string {
  const known = KNOWN_INITIALISMS.get(word.toLowerCase());
  if (known) return known;
  if (word === word.toUpperCase() && word.length > 1) return word;
  return word.charAt(0).toUpperCase() + word.slice(1);
}

export function formatDisplayName(name: string | null | undefined): string {
  const trimmed = name?.trim();
  if (!trimmed) return "";

  return trimmed
    .replace(/[_-]+/g, " ")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .split(/\s+/)
    .filter(Boolean)
    .map(formatWord)
    .join(" ");
}

export function getSchemaDisplayName(schema: LabelSchema | null | undefined, fallback = "Model"): string {
  return formatDisplayName(schema?.displayName ?? schema?.name) || fallback;
}

function readEntityLabel(entity: Record<string, unknown>, field: string): string | null {
  const directValue = getSchemaFieldValue(entity, field);
  const event = isRecord(entity["event"]) ? entity["event"] : null;
  const eventValue = event ? getSchemaFieldValue(event, field) : undefined;
  const value = directValue ?? eventValue;

  if (typeof value === "string" && value.trim().length > 0) return value.trim();
  if (typeof value === "number" && Number.isFinite(value)) return value.toString();
  return null;
}

export function getEntityDisplayName(
  schema: LabelSchema | null | undefined,
  entity: Record<string, unknown> | null | undefined,
  fallback: string
): string {
  if (!entity) return fallback;

  const schemaFields = schema ? generateColumnsFromSchema(schema as LoyaltySchema).map((column) => column.key) : [];
  const fields = Array.from(new Set([...ENTITY_LABEL_FIELDS, ...schemaFields, "id"]));

  for (const field of fields) {
    const value = readEntityLabel(entity, field);
    if (value) return value;
  }

  return fallback;
}
