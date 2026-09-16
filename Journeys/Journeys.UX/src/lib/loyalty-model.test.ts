import { describe, expect, it } from "vitest";
import {
  getManyModelsListBody,
  isUsableModelId,
  loyaltyScreenModel,
  LOYALTY_MODEL_TYPE
} from "./loyalty-model";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";

describe("getManyModelsListBody", () => {
  it("sends modelType loyalty and no modelIds", () => {
    const body = getManyModelsListBody(100);
    expect(body.modelType).toBe(LOYALTY_MODEL_TYPE);
    expect(body.pageSize).toBe(100);
    expect(body).not.toHaveProperty("modelIds");
    expect(body).not.toHaveProperty("modelId");
  });
});

describe("isUsableModelId", () => {
  it("rejects empty and unknown", () => {
    expect(isUsableModelId(undefined)).toBe(false);
    expect(isUsableModelId("")).toBe(false);
    expect(isUsableModelId("unknown")).toBe(false);
    expect(isUsableModelId("Unknown")).toBe(false);
  });

  it("accepts a real id or schema name", () => {
    expect(isUsableModelId("LoyaltyAccountDetails")).toBe(true);
    expect(isUsableModelId("a6edbbc5-bf43-4c57-b2f1-e015b9efaf03")).toBe(true);
  });
});

describe("loyaltyScreenModel", () => {
  it("maps Accounts to LoyaltyAccountDetails by name, never unknown", () => {
    const model = loyaltyScreenModel("accounts");
    expect(model.modelType).toBe(LOYALTY_MODEL_TYPE);
    expect(model.schemaName).toBe(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
    expect(isUsableModelId(model.schemaName)).toBe(true);
    expect(model.schemaName.toLowerCase()).not.toBe("unknown");
  });

  it("maps Campaigns to the platform campaign list (API supplies the campaign model GUID)", () => {
    const model = loyaltyScreenModel("campaigns");
    expect(model.modelType).toBe(LOYALTY_MODEL_TYPE);
    expect(model.usesPlatformCampaignModel).toBe(true);
  });
});
