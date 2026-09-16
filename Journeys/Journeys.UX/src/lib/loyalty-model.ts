import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";

export const LOYALTY_MODEL_TYPE = "loyalty";

export type LoyaltyScreen = "accounts" | "campaigns";

export type LoyaltyScreenModel = {
  modelType: string;
  schemaName?: string;
  usesPlatformCampaignModel?: boolean;
};

/** List-all models of type loyalty. Never send ModelIds (Backend rejects unknown/empty model ids). */
export function getManyModelsListBody(pageSize = 500): { modelType: string; pageSize: number } {
  return { modelType: LOYALTY_MODEL_TYPE, pageSize };
}

export function isUsableModelId(value: string | undefined): boolean {
  const v = value?.trim() ?? "";
  return v.length > 0 && v.toLowerCase() !== "unknown";
}

/** Each Loyalty screen owns its model. Do not invent "unknown". */
export function loyaltyScreenModel(screen: LoyaltyScreen): LoyaltyScreenModel {
  if (screen === "accounts") {
    return {
      modelType: LOYALTY_MODEL_TYPE,
      schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME
    };
  }
  return { modelType: LOYALTY_MODEL_TYPE, usesPlatformCampaignModel: true };
}
