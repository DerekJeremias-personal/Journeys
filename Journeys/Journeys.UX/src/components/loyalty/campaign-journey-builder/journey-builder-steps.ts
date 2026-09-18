import type { LucideIcon } from "lucide-react";
import { Filter, Rocket, Target, Users, Zap } from "lucide-react";

export type JourneyBuilderStepKey = "setup" | "eligibility" | "criteria" | "actions" | "review";

export interface JourneyBuilderStepMeta {
  readonly key: JourneyBuilderStepKey;
  readonly number: number;
  readonly title: string;
  readonly description: string;
  readonly icon: LucideIcon;
}

export const JOURNEY_BUILDER_STEPS: readonly JourneyBuilderStepMeta[] = [
  {
    key: "setup",
    number: 1,
    title: "Campaign Setup",
    description: "Name, type & schedule",
    icon: Target,
  },
  {
    key: "eligibility",
    number: 2,
    title: "Eligibility",
    description: "Audience to target",
    icon: Users,
  },
  {
    key: "criteria",
    number: 3,
    title: "Audience Criteria",
    description: "Purchase & event rules",
    icon: Filter,
  },
  {
    key: "actions",
    number: 4,
    title: "Resulting Actions",
    description: "What happens",
    icon: Zap,
  },
  {
    key: "review",
    number: 5,
    title: "Review & Launch",
    description: "Visual campaign path",
    icon: Rocket,
  },
] as const;
