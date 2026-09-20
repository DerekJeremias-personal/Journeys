import type { SchemaListItem } from "@/lib/api-types";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";

export const REQUIRED_EVENT_MODEL_METADATA_KEYS = [
  "Wrapper",
  "NaturalKeySymbols",
  "AccountXIdSymbol",
  "TimeOfOccurrence"
] as const;

export function includeInModelBuilderCatalog(schema: SchemaListItem): boolean {
  const type = schema.modelType?.toLowerCase();
  if (type && type !== "loyalty") return false;
  if (schema.tag?.toLowerCase() === "eventable") return true;
  return schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
}

export function missingEventModelMetadata(schema: SchemaListItem): string[] {
  const meta = schema.modelMetaData ?? {};
  return REQUIRED_EVENT_MODEL_METADATA_KEYS.filter((key) => !meta[key]?.trim());
}

export function buildModelBuilderBrief(args: {
  tenantId: string;
  schemas: SchemaListItem[];
  catalogUnavailable?: boolean;
}): string {
  const lines: string[] = [
    `Opened from Journeys.UX for tenant ${args.tenantId}. Author event-signal (processable) models for this tenant.`,
    "",
    "## Contract",
    "- Payload vs wrapper: customer JSON is WrappedEventPayload.event; the wrapper is the engine envelope.",
    "- Processable models need metadata: Wrapper (wrapper model id), NaturalKeySymbols, AccountXIdSymbol (payload-root path unless loyalty-account-shaped), TimeOfOccurrence.",
    "- Persist wrapper symbols lowercase: naturalkey, timeofoccurrence, lastprocessed, accountid, appliedcampaigns, appliedrulesetids, providerstates, journeystates, outcomestates.",
    "- Campaign.Events lists payload model GUIDs, not display names.",
    "- modelType loyalty. Processable payload models use tag eventable.",
    "- Never use modelId \"unknown\".",
    "",
    "## Catalog snapshot"
  ];
  if (args.catalogUnavailable) {
    lines.push("Catalog snapshot unavailable");
  } else {
    const rows = args.schemas.filter(includeInModelBuilderCatalog);
    if (rows.length === 0) {
      lines.push("(no loyalty eventable models in GetMany)");
    }
    for (const schema of rows) {
      const missing = missingEventModelMetadata(schema);
      const gap = missing.length > 0 ? `missing: ${missing.join(", ")}` : "ok";
      lines.push(
        `- ${schema.name ?? "(unnamed)"} id=${schema.id ?? ""} status=${schema.status ?? ""} tag=${schema.tag ?? ""} ${gap}`
      );
    }
  }
  return lines.join("\n");
}
