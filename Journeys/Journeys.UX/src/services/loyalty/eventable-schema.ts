import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";

/** A schema whose rows are account-scoped events we can show inline on a detail page. */
export function isEventableSchema(schema: LoyaltySchema): boolean {
  return schema.tag === "eventable" || schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
}
