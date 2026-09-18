/**
 * Campaign Wizard templates — ported verbatim from the legacy
 * `admin-web/src/services/loyalty/components/campaigns-v2/Templates/templates/*`
 * (birthday-bonus, points-per-dollar, referral-program, tier-progression).
 *
 * Each template is a pure factory returning a `Partial<Campaign>` so that
 * `<TemplatesPicker>` can hydrate the wizard form with a fresh set of UUIDs
 * each time. Visual metadata (icon, color, tags, preview) lives alongside.
 */

import type { Campaign, Journey } from "@/lib/campaign-types";

export type TemplateId =
  | "points-per-dollar"
  | "double-points"
  | "welcome-bonus"
  | "tier-progression"
  | "birthday-bonus"
  | "referral-program"
  | "category-bonus"
  | "spend-threshold"
  | "review-rewards"
  | "anniversary-reward";

export type TemplateTone = "primary" | "success" | "warning" | "secondary";

export type TemplateCategory = "Earning" | "Promotion" | "Acquisition" | "Loyalty Tiers" | "Engagement" | "Retention";

export interface CampaignTemplate {
  id: TemplateId;
  name: string;
  description: string;
  /** A single emoji glyph used inline; safe to render via text. */
  icon: string;
  tone: TemplateTone;
  category: TemplateCategory;
  tags: ReadonlyArray<string>;
  preview: string;
  createCampaign: () => Partial<Campaign>;
}

function makeJourneyNode(id: string, name: string, rootNodeId: string, children: Journey[] = []): Journey {
  return {
    id,
    name,
    rootNodeId,
    rules: [],
    navigation: {},
    children,
    tenantId: null,
  };
}

/** Helper: build a minimal `Partial<Campaign>` with a single-node journey. */
function singleNodeCampaign(name: string, eventType: string, journeyName?: string): Partial<Campaign> {
  const journeyId = crypto.randomUUID();
  return {
    name,
    extCampaignId: name
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, ""),
    status: "draft",
    startDate: new Date().toISOString(),
    events: [eventType],
    segments: [],
    journey: makeJourneyNode(journeyId, journeyName ?? name, journeyId),
  };
}

const pointsPerDollar: CampaignTemplate = {
  id: "points-per-dollar",
  name: "Points Per Dollar",
  description: "Simple earning campaign that awards points based on purchase amount.",
  icon: "💰",
  tone: "success",
  category: "Earning",
  tags: ["Basic", "Earning", "Purchase"],
  preview: "Earn 1 point for every $1 spent",
  createCampaign: () => singleNodeCampaign("Points Per Dollar Campaign", "Purchase", "Points Earning"),
};

const doublePoints: CampaignTemplate = {
  id: "double-points",
  name: "Double Points Weekend",
  description: "Boost engagement with 2x points during promotional periods.",
  icon: "✨",
  tone: "secondary",
  category: "Promotion",
  tags: ["Bonus", "Weekend", "Multiplier"],
  preview: "Earn 2x points on weekends",
  createCampaign: () => singleNodeCampaign("Double Points Weekend", "Purchase", "Weekend Promotion"),
};

const welcomeBonus: CampaignTemplate = {
  id: "welcome-bonus",
  name: "Welcome Bonus",
  description: "Reward new customers with bonus points on their first qualifying purchase.",
  icon: "🎁",
  tone: "primary",
  category: "Acquisition",
  tags: ["New Customer", "Onboarding", "Bonus"],
  preview: "500 bonus points on first $50+ purchase",
  createCampaign: () => singleNodeCampaign("Welcome Bonus Campaign", "Purchase", "First Purchase Reward"),
};

const tierProgression: CampaignTemplate = {
  id: "tier-progression",
  name: "Tier Progression",
  description: "Multi-tier loyalty program with Bronze, Silver, Gold, and Platinum levels.",
  icon: "🏆",
  tone: "warning",
  category: "Loyalty Tiers",
  tags: ["Tiers", "Progression", "Status"],
  preview: "Progress through Bronze → Silver → Gold → Platinum based on lifetime points",
  createCampaign: () => {
    const rootId = crypto.randomUUID();
    return {
      name: "Tier Progression Program",
      extCampaignId: "tier-progression-program",
      status: "draft",
      startDate: new Date().toISOString(),
      events: ["Purchase"],
      segments: [],
      journey: makeJourneyNode(rootId, "Tier Program", rootId, [
        makeJourneyNode(crypto.randomUUID(), "Bronze Tier", rootId),
        makeJourneyNode(crypto.randomUUID(), "Silver Tier", rootId),
        makeJourneyNode(crypto.randomUUID(), "Gold Tier", rootId),
        makeJourneyNode(crypto.randomUUID(), "Platinum Tier", rootId),
      ]),
    };
  },
};

const birthdayBonus: CampaignTemplate = {
  id: "birthday-bonus",
  name: "Birthday Bonus",
  description: "Award bonus points during the customer's birthday month.",
  icon: "🎂",
  tone: "secondary",
  category: "Engagement",
  tags: ["Special Event", "Birthday", "Bonus"],
  preview: "Double points during birthday month + 500 bonus points",
  createCampaign: () => singleNodeCampaign("Birthday Bonus Campaign", "BirthdayEvent", "Birthday Bonus"),
};

const referralProgram: CampaignTemplate = {
  id: "referral-program",
  name: "Referral Program",
  description: "Reward both referrer and referee for successful referrals.",
  icon: "🤝",
  tone: "primary",
  category: "Acquisition",
  tags: ["Referral", "Growth", "Viral"],
  preview: "Referrer gets 500 points, referee gets 250 points on first purchase",
  createCampaign: () => {
    const rootId = crypto.randomUUID();
    const referrerId = crypto.randomUUID();
    const refereeId = crypto.randomUUID();
    return {
      name: "Referral Program",
      extCampaignId: "referral-program",
      status: "draft",
      startDate: new Date().toISOString(),
      events: ["ReferralComplete", "Purchase"],
      segments: [],
      journey: makeJourneyNode(rootId, "Referral Program", rootId, [
        makeJourneyNode(referrerId, "Referrer Reward", rootId),
        makeJourneyNode(refereeId, "Referee Welcome Bonus", rootId),
      ]),
    };
  },
};

const categoryBonus: CampaignTemplate = {
  id: "category-bonus",
  name: "Category Bonus",
  description: "Extra points when shopping specific product categories.",
  icon: "🏷️",
  tone: "primary",
  category: "Promotion",
  tags: ["Category", "Products", "Targeted"],
  preview: "3x points on selected categories",
  createCampaign: () => singleNodeCampaign("Category Bonus Campaign", "Purchase", "Category Promotion"),
};

const spendThreshold: CampaignTemplate = {
  id: "spend-threshold",
  name: "Spend Threshold Bonus",
  description: "Reward customers who reach spending milestones.",
  icon: "📈",
  tone: "warning",
  category: "Engagement",
  tags: ["Threshold", "Milestone", "Tiered"],
  preview: "100 bonus points for every $100 spent",
  createCampaign: () => singleNodeCampaign("Spend Threshold Campaign", "Purchase", "Spend Milestone"),
};

const reviewRewards: CampaignTemplate = {
  id: "review-rewards",
  name: "Review Rewards",
  description: "Encourage product reviews with point incentives.",
  icon: "⭐",
  tone: "warning",
  category: "Engagement",
  tags: ["Review", "Feedback", "UGC"],
  preview: "50 points per verified review",
  createCampaign: () => singleNodeCampaign("Review Rewards Campaign", "Review", "Review Reward"),
};

const anniversaryReward: CampaignTemplate = {
  id: "anniversary-reward",
  name: "Anniversary Reward",
  description: "Celebrate membership anniversaries with special rewards.",
  icon: "🎉",
  tone: "primary",
  category: "Retention",
  tags: ["Anniversary", "Loyalty", "Retention"],
  preview: "Bonus points on membership anniversary",
  createCampaign: () => singleNodeCampaign("Anniversary Reward Campaign", "AnniversaryEvent", "Anniversary"),
};

export const WIZARD_TEMPLATES: ReadonlyArray<CampaignTemplate> = [
  pointsPerDollar,
  doublePoints,
  welcomeBonus,
  tierProgression,
  birthdayBonus,
  referralProgram,
  categoryBonus,
  spendThreshold,
  reviewRewards,
  anniversaryReward,
] as const;

export const WIZARD_TEMPLATE_CATEGORIES: ReadonlyArray<TemplateCategory | "All"> = [
  "All",
  "Earning",
  "Promotion",
  "Acquisition",
  "Loyalty Tiers",
  "Engagement",
  "Retention",
] as const;
