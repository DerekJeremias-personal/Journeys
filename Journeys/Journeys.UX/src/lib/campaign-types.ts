import { z } from "zod";

export * from "./campaign-rule-types";
export type { Rule as LoyaltyRule } from "./campaign-rule-types";
export { RuleSchema as LoyaltyRuleSchema } from "./campaign-rule-types";
export type { TaxonomyDto } from "./campaign-taxonomy-types";
export { TaxonomyDtoSchema } from "./campaign-taxonomy-types";

import type { Outcome } from "./campaign-rule-types";

const DtoModelBaseFields = {
  tenantId: z.string().nullable().optional(),
  id: z.string().nullable().optional(),
  etag: z.string().nullable().optional()
} as const;

export const CampaignStatusSchema = z.enum(["draft", "live", "pause", "archive"]);
export type CampaignStatus = z.infer<typeof CampaignStatusSchema>;

export const SegmentSourceSchema = z.object({
  name: z.string(),
  status: z.string(),
  type: z.string(),
  sourceFolder: z.string().nullable().optional(),
  sourceFile: z.string().nullable().optional(),
  apiUrl: z.string().nullable().optional(),
  query: z.string().nullable().optional(),
  lastRunDate: z.string().nullable().optional(),
  lastRunDuration: z.number().nullable().optional()
});
export type SegmentSource = z.infer<typeof SegmentSourceSchema>;

export const ScheduleSchema = z.object({
  ...DtoModelBaseFields,
  name: z.string(),
  status: z.string(),
  type: z.string(),
  startDate: z.string(),
  endDate: z.string().nullable().optional(),
  frequency: z.number().nullable().optional(),
  frequencyUnit: z.string().nullable().optional(),
  lastRunDate: z.string().nullable().optional(),
  lastRunDuration: z.number().nullable().optional(),
  nextRunDate: z.string().nullable().optional()
});
export type Schedule = z.infer<typeof ScheduleSchema>;

export const LoyaltySegmentSchema = z.object({
  ...DtoModelBaseFields,
  extSegmentId: z.string(),
  name: z.string(),
  status: z.string(),
  type: z.string(),
  targetFolder: z.string().nullable().optional(),
  targetFile: z.string().nullable().optional(),
  fileCreateDate: z.string().nullable().optional(),
  source: SegmentSourceSchema.nullable().optional(),
  schedule: ScheduleSchema.nullable().optional()
});
export type LoyaltySegment = z.infer<typeof LoyaltySegmentSchema>;

export type RuleSet = {
  tenantId?: string | null;
  id?: string | null;
  etag?: string | null;
  name?: string | null;
  ruleJsonElement?: unknown;
  rootRuleDiscriminator?: string | null;
  outcomesJsonElement?: unknown;
  schemaId?: string | null;
  rules?: unknown[];
  outcomes?: Outcome[];
  [key: string]: unknown;
};

export type Journey = {
  tenantId?: string | null;
  id?: string | null;
  etag?: string | null;
  name?: string | null;
  rootNodeId?: string | null;
  children?: Journey[] | null;
  rules?: RuleSet[] | null;
  ruleSets?: RuleSet[];
  navigation?: unknown;
  [key: string]: unknown;
};

export type LoyaltySchema = {
  id: string;
  name: string;
  tenantId?: string;
  status?: string;
  tag?: string | null;
  modelType?: string;
  attributes?: { name?: string; displayName?: string; symbol?: string; dataType?: string; [key: string]: unknown }[];
  [key: string]: unknown;
};

export type PointAccountType = {
  id?: string | null;
  name: string;
  tenantId?: string | null;
  etag?: string | null;
  [key: string]: unknown;
};

export type Campaign = {
  id?: string | null;
  tenantId?: string | null;
  etag?: string | null;
  name?: string;
  status?: string;
  extCampaignId?: string | null;
  startDate?: string;
  endDate?: string | null;
  events?: string[] | null;
  segments?: LoyaltySegment[] | null;
  journey?: Journey | null;
  [key: string]: unknown;
};

export type AgentBuilderPhase =
  | "idle"
  | "setup"
  | "eligibility"
  | "criteria"
  | "actions"
  | "review";

export function asLoyaltySchemas(items: readonly unknown[] | undefined): LoyaltySchema[] {
  const out: LoyaltySchema[] = [];
  for (const item of items ?? []) {
    if (!item || typeof item !== "object") continue;
    const rec = item as Record<string, unknown>;
    if (typeof rec.id !== "string" || typeof rec.name !== "string") continue;
    out.push({ ...rec, id: rec.id, name: rec.name });
  }
  return out;
}

export function asPointAccountTypes(items: readonly unknown[] | undefined): PointAccountType[] {
  const out: PointAccountType[] = [];
  for (const item of items ?? []) {
    if (!item || typeof item !== "object") continue;
    const rec = item as Record<string, unknown>;
    if (typeof rec.name !== "string") continue;
    out.push({
      ...rec,
      name: rec.name,
      id: typeof rec.id === "string" ? rec.id : null
    });
  }
  return out;
}
