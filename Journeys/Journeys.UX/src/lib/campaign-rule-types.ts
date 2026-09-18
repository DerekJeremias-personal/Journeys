/**
 * Loyalty rules-engine discriminated unions — `loyalty` upstream (raw `JsonElement` inner shapes).
 *
 * Source-of-truth mapping:
 *   - C# discriminators: Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/RuleBase.cs
 *     (`RuleKindDiscriminators`, `ProviderKindDiscriminators`, `OutcomeKindDiscriminators`)
 *   - C# rule classes: Elevate.ELP.Core/RulesEngine/Rules/{Composite/,*.cs}
 *   - C# provider classes: Elevate.ELP.Core/RulesEngine/Providers/*.cs
 *   - C# evaluator classes: Elevate.ELP.Core/RulesEngine/Comparitors/*.cs (+ Enums/)
 *   - C# outcome classes: Elevate.ELP.Core/RulesEngine/Outcomes/*.cs
 *   - C# journey navigation: Elevate.ELP.Core/RulesEngine/Journey/SimpleNavigationCriteria.cs
 *
 * Wire reality (Round 4 — verified against `seeds/captures/loyalty/hayward/campaign-get.json`):
 *   - These shapes appear inside ELP responses as raw `JsonElement` columns preserved verbatim
 *     from Cosmos. **Keys are PascalCase** (e.g. `Kind`, `LeftProvider`, `RightProvider`,
 *     `RuleJsonElement`, `OutcomesJsonElement`, `Navigation`, `NavConstraint`, `Outcomes`).
 *   - The `$type` field is a STJ polymorphism marker. For `PointBalanceProvider` the wire ships
 *     `Kind: "PathValueProvider"` (parent's literal — server-side bug preserved as wire contract)
 *     AND `$type: "PointBalanceProvider"` (the actual class). The discriminator we trust is
 *     `Kind`; consumers needing point-balance semantics detect `pointAccountTypeId` on a
 *     `PathValueProvider` variant.
 *   - `Comparison` ships as **either** an integer (Cosmos archive write) **or** a string literal
 *     (newer `JsonStringEnumConverter` writes). Schemas accept both via `z.union`.
 *
 * Consumer parse pattern (per `RuleSet.ruleJsonElement` / `outcomesJsonElement`,
 *   `Journey.navigation` typed `z.unknown()` in `loyalty.ts`):
 *
 *     const rule = RuleJsonElementSchema.parse(ruleSet.ruleJsonElement);
 *     const outcomes = OutcomeJsonElementSchema.parse(ruleSet.outcomesJsonElement);
 *     const nav = NavigationSchema.parse(journey.navigation);
 */
import { z } from "zod";

// ─────────────────────────────────────────────────────────────────────────────
// 2.1 Evaluation enums (shared across rules + outcomes)
// ─────────────────────────────────────────────────────────────────────────────

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/Enums/StringEvalType.cs */
export const StringEvalTypeSchema = z.enum([
  "Unassigned",
  "Equal",
  "Contains",
  "StartsWith",
  "EndsWith",
  "MatchesRegex",
  "InCollection",
  "NotEqual",
]);
export type StringEvalType = z.infer<typeof StringEvalTypeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/Enums/NumEvalType.cs */
export const NumEvalTypeSchema = z.enum([
  "Unassigned",
  "Equal",
  "GreaterThan",
  "GreaterThanOrEqual",
  "LessThan",
  "LessThanOrEqual",
]);
export type NumEvalType = z.infer<typeof NumEvalTypeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/Enums/DateEvalType.cs */
export const DateEvalTypeSchema = z.enum([
  "Unassigned",
  "Equal",
  "GreaterThan",
  "GreaterThanOrEqual",
  "LessThan",
  "LessThanOrEqual",
]);
export type DateEvalType = z.infer<typeof DateEvalTypeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/Enums/TemporalEvalType.cs */
export const TemporalEvalTypeSchema = z.enum(["Unassigned", "OnOrBefore", "Before", "OnOrAfter", "After"]);
export type TemporalEvalType = z.infer<typeof TemporalEvalTypeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Providers/Enums/AggregateType.cs */
export const AggregateTypeSchema = z.enum(["Undefined", "Sum", "Average", "Min", "Max", "Count"]);
export type AggregateType = z.infer<typeof AggregateTypeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Journey/Enums/NavigationType.cs */
export const NavigationTypeSchema = z.enum(["Entry", "Exit", "Transition"]);
export type NavigationType = z.infer<typeof NavigationTypeSchema>;

/**
 * Wire-tolerant comparison field — ships as integer (Cosmos archive) OR string (JsonStringEnumConverter).
 * Consumers that need string normalization should map via the Enum[N] reverse lookup.
 */
const ComparisonValue = z.union([z.number().int(), z.string()]);

// ─────────────────────────────────────────────────────────────────────────────
// 2.2 Provider discriminated union
// ─────────────────────────────────────────────────────────────────────────────

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/RuleBase.cs (`ProviderKindDiscriminators`) */
export const ProviderKindSchema = z.enum([
  "SimpleCalculationProvider",
  "AggregateValueProvider",
  "ConstantValueProvider",
  "LineageValueProvider",
  "PathValueProvider",
]);
export type ProviderKind = z.infer<typeof ProviderKindSchema>;

/**
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Providers/PathValueProvider.cs
 *
 * `PropertyPath` is `optional` because `PointBalanceProvider` re-uses
 * `Kind = "PathValueProvider"` (server-side bug per `PointBalanceProvider.cs:16`) and ships
 * `PointAccountTypeId` instead of `PropertyPath`. Wire-verified across the campaign-get fixture.
 * Consumers needing point-balance semantics detect `PointAccountTypeId` presence.
 */
export const PathValueProviderSchema = z.object({
  Kind: z.literal("PathValueProvider"),
  $type: z.string().optional(),
  PropertyPath: z.string().optional(),
  PointAccountTypeId: z.string().optional(),
});
export type PathValueProvider = z.infer<typeof PathValueProviderSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Providers/ConstantValueProvider.cs */
export const ConstantValueProviderSchema = z.object({
  Kind: z.literal("ConstantValueProvider"),
  $type: z.string().optional(),
  /** `JsonElement` on the server; preserve verbatim. */
  Value: z.unknown().nullable().optional(),
});
export type ConstantValueProvider = z.infer<typeof ConstantValueProviderSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Providers/LineageValueProvider.cs */
export const LineageValueProviderSchema = z.object({
  Kind: z.literal("LineageValueProvider"),
  $type: z.string().optional(),
  PropertyPath: z.string(),
});
export type LineageValueProvider = z.infer<typeof LineageValueProviderSchema>;

/**
 * Server-side stub — `SimpleCalculationProvider` is registered in `ProviderKindDiscriminators`
 * but no concrete `.cs` implementation populates extra fields beyond the base `Kind`. Schema
 * mirrors that minimum surface; extend if a real fixture grows the shape.
 */
export const SimpleCalculationProviderSchema = z.object({
  Kind: z.literal("SimpleCalculationProvider"),
  $type: z.string().optional(),
});
export type SimpleCalculationProvider = z.infer<typeof SimpleCalculationProviderSchema>;

/**
 * Recursive — `RowProvider`, `RowPropertyProvider`, and `Constraint` reference
 * `ProviderSchema` / `RuleSchema`. Hoisted via `z.lazy`.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Providers/AggregateValueProvider.cs
 */
export type AggregateValueProvider = {
  Kind: "AggregateValueProvider";
  $type?: string | undefined;
  AggregateType: AggregateType | number | string;
  RowProvider?: Provider | null | undefined;
  RowPropertyProvider?: Provider | null | undefined;
  Constraint?: Rule | null | undefined;
};
export const AggregateValueProviderSchema: z.ZodType<AggregateValueProvider> = z.lazy(() =>
  z.object({
    Kind: z.literal("AggregateValueProvider"),
    $type: z.string().optional(),
    /** Wire ships either AggregateType enum-as-int, AggregateType name string, or already-mapped value. */
    AggregateType: z.union([AggregateTypeSchema, z.number().int(), z.string()]),
    RowProvider: ProviderSchema.nullable().optional(),
    RowPropertyProvider: ProviderSchema.nullable().optional(),
    Constraint: RuleSchema.nullable().optional(),
  })
);

/**
 * Provider union (raw JsonElement inner; PascalCase keys per Cosmos verbatim).
 * Discriminator: `Kind`.
 *
 * Implementation note: uses `z.union` rather than `z.discriminatedUnion` because the
 * recursive `AggregateValueProvider` variant — which references `ProviderSchema` /
 * `RuleSchema` via `z.lazy` — erases its discriminator `propValues` at the type level.
 * Parse semantics are equivalent (each variant tried until one matches the literal
 * `Kind` field); only the parser-internal fast-path differs.
 */
export type Provider =
  | PathValueProvider
  | ConstantValueProvider
  | LineageValueProvider
  | SimpleCalculationProvider
  | AggregateValueProvider;
export const ProviderSchema: z.ZodType<Provider> = z.lazy(() =>
  z.union([
    PathValueProviderSchema,
    ConstantValueProviderSchema,
    LineageValueProviderSchema,
    SimpleCalculationProviderSchema,
    AggregateValueProviderSchema,
  ])
);

// ─────────────────────────────────────────────────────────────────────────────
// 2.4 Evaluator (declared before Rule — consumed by SimpleRule + leaf rules)
// ─────────────────────────────────────────────────────────────────────────────

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/StringEvaluation.cs */
export const StringEvaluationSchema = z.object({
  $type: z.literal("StringEvaluation").optional(),
  Comparison: ComparisonValue,
});
export type StringEvaluation = z.infer<typeof StringEvaluationSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/NumericEvaluation.cs */
export const NumericEvaluationSchema = z.object({
  $type: z.literal("NumericEvaluation").optional(),
  Comparison: ComparisonValue,
});
export type NumericEvaluation = z.infer<typeof NumericEvaluationSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/DateEvaluation.cs */
export const DateEvaluationSchema = z.object({
  $type: z.literal("DateEvaluation").optional(),
  Comparison: ComparisonValue,
});
export type DateEvaluation = z.infer<typeof DateEvaluationSchema>;

/**
 * Note field name `ComparisonType` (NOT `Comparison`) — diverges from sibling evaluators.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/TemporalEvaluation.cs
 */
export const TemporalEvaluationSchema = z.object({
  $type: z.literal("TemporalEvaluation").optional(),
  Comparison: ComparisonValue.optional(),
  ComparisonType: ComparisonValue.optional(),
});
export type TemporalEvaluation = z.infer<typeof TemporalEvaluationSchema>;

/**
 * `BoolEvaluation` has no fields in the C# class but the wire ships `Comparison: "Equal"` for
 * some campaigns. Accept it as optional.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Comparitors/BoolEvaluation.cs
 */
export const BoolEvaluationSchema = z.object({
  $type: z.literal("BoolEvaluation").optional(),
  Comparison: ComparisonValue.optional(),
});
export type BoolEvaluation = z.infer<typeof BoolEvaluationSchema>;

export const EvaluatorSchema = z.union([
  StringEvaluationSchema,
  NumericEvaluationSchema,
  DateEvaluationSchema,
  TemporalEvaluationSchema,
  BoolEvaluationSchema,
]);
export type Evaluator = z.infer<typeof EvaluatorSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// 2.3 Rule discriminated union
// ─────────────────────────────────────────────────────────────────────────────

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/RuleBase.cs (`RuleKindDiscriminators`) */
export const RuleKindSchema = z.enum([
  "AndRule",
  "OrRule",
  "NotRule",
  "SimpleRule",
  "DatePropertyRule",
  "HistoricalRule",
  "NumericPropertyRule",
  "StringPropertyRule",
  "TemporalConstraintRule",
  "TaxonomicRule",
]);
export type RuleKind = z.infer<typeof RuleKindSchema>;

/** Common fields for every leaf rule (SimpleRule + its specializations). */
const LeafRuleBaseFields = {
  Id: z.string().optional(),
  $type: z.string().optional(),
  LeftProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  RightProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  Evaluator: z
    .lazy(() => EvaluatorSchema)
    .nullable()
    .optional(),
} as const;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/SimpleRule.cs */
export const SimpleRuleSchema = z.object({
  Kind: z.literal("SimpleRule"),
  ...LeafRuleBaseFields,
});
export type SimpleRule = z.infer<typeof SimpleRuleSchema>;

/**
 * Specialization of `SimpleRule<decimal>`; same field set, different default evaluator.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/DatePropertyRule.cs
 */
export const DatePropertyRuleSchema = z.object({
  Kind: z.literal("DatePropertyRule"),
  ...LeafRuleBaseFields,
});
export type DatePropertyRule = z.infer<typeof DatePropertyRuleSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/NumericPropertyRule.cs */
export const NumericPropertyRuleSchema = z.object({
  Kind: z.literal("NumericPropertyRule"),
  ...LeafRuleBaseFields,
});
export type NumericPropertyRule = z.infer<typeof NumericPropertyRuleSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/StringPropertyRule.cs */
export const StringPropertyRuleSchema = z.object({
  Kind: z.literal("StringPropertyRule"),
  ...LeafRuleBaseFields,
});
export type StringPropertyRule = z.infer<typeof StringPropertyRuleSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/TaxonomicRule.cs */
export const TaxonomicRuleSchema = z.object({
  Kind: z.literal("TaxonomicRule"),
  ...LeafRuleBaseFields,
  IncludedTreeNodes: z.array(z.string()).nullable().optional(),
  IncludedIds: z.array(z.string()).nullable().optional(),
  ExcludedTreeNodes: z.array(z.string()).nullable().optional(),
  ExcludedIds: z.array(z.string()).nullable().optional(),
  TaxonomyType: z.string(),
  TaxonomyId: z.string(),
  KeySymbolPath: z.string(),
});
export type TaxonomicRule = z.infer<typeof TaxonomicRuleSchema>;

/**
 * `HistoricalValueProvider` is the `IHistoricalValueProvider` interface; no concrete DTO on the
 * server yet — keep `z.unknown()`.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/HistoricalRule.cs
 */
export type HistoricalRule = {
  Kind: "HistoricalRule";
  Id?: string | undefined;
  $type?: string | undefined;
  AggregateType: AggregateType | number | string;
  AggregationValueProvider?: Provider | null | undefined;
  IsApplicableConstraint?: Rule | null | undefined;
  HistoricalValueProvider?: unknown;
  RightProvider?: Provider | null | undefined;
};
export const HistoricalRuleSchema: z.ZodType<HistoricalRule> = z.lazy(() =>
  z.object({
    Kind: z.literal("HistoricalRule"),
    Id: z.string().optional(),
    $type: z.string().optional(),
    AggregateType: z.union([AggregateTypeSchema, z.number().int(), z.string()]),
    AggregationValueProvider: ProviderSchema.nullable().optional(),
    IsApplicableConstraint: RuleSchema.nullable().optional(),
    HistoricalValueProvider: z.unknown().optional(),
    RightProvider: ProviderSchema.nullable().optional(),
  })
);

/**
 * `Comparison` is `TimeSpan` on the server → ISO-8601 duration or `HH:MM:SS` string on the wire.
 *
 * Wire-tolerance note: The nested `TemporalEvaluation` object uses the `Comparison` property name
 * per `TemporalEvaluation.cs`. Some Cosmos archive writes may use `ComparisonType` instead.
 * We accept both via `TemporalEvaluationSchema` which already handles both fields as optional.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/TemporalConstraintRule.cs
 */
export const TemporalConstraintRuleSchema = z.object({
  Kind: z.literal("TemporalConstraintRule"),
  Id: z.string().optional(),
  $type: z.string().optional(),
  TimeOfOccurrenceProvider: PathValueProviderSchema,
  /** Accepts both `Comparison` (C# canonical) and `ComparisonType` (archive variant). */
  TemporalEvaluation: TemporalEvaluationSchema,
  Comparison: z.string(),
});
export type TemporalConstraintRule = z.infer<typeof TemporalConstraintRuleSchema>;

// Composite rules (AndRule / OrRule / NotRule). Children is recursive.
/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/Composite/AndRule.cs */
export type AndRule = {
  Kind: "AndRule";
  Id?: string | undefined;
  $type?: string | undefined;
  Children: Array<Rule>;
};
export const AndRuleSchema: z.ZodType<AndRule> = z.lazy(() =>
  z.object({
    Kind: z.literal("AndRule"),
    Id: z.string().optional(),
    $type: z.string().optional(),
    Children: z.array(RuleSchema),
  })
);

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/Composite/OrRule.cs */
export type OrRule = {
  Kind: "OrRule";
  Id?: string | undefined;
  $type?: string | undefined;
  Children: Array<Rule>;
};
export const OrRuleSchema: z.ZodType<OrRule> = z.lazy(() =>
  z.object({
    Kind: z.literal("OrRule"),
    Id: z.string().optional(),
    $type: z.string().optional(),
    Children: z.array(RuleSchema),
  })
);

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/Composite/NotRule.cs (children typically length 1) */
export type NotRule = {
  Kind: "NotRule";
  Id?: string | undefined;
  $type?: string | undefined;
  Children: Array<Rule>;
};
export const NotRuleSchema: z.ZodType<NotRule> = z.lazy(() =>
  z.object({
    Kind: z.literal("NotRule"),
    Id: z.string().optional(),
    $type: z.string().optional(),
    Children: z.array(RuleSchema),
  })
);

export type Rule =
  | SimpleRule
  | DatePropertyRule
  | NumericPropertyRule
  | StringPropertyRule
  | TaxonomicRule
  | HistoricalRule
  | TemporalConstraintRule
  | AndRule
  | OrRule
  | NotRule;
/**
 * Implementation note: uses `z.union` (see `ProviderSchema` JSDoc for the same reason).
 * Parse semantics are equivalent.
 */
export const RuleSchema: z.ZodType<Rule> = z.lazy(() =>
  z.union([
    SimpleRuleSchema,
    DatePropertyRuleSchema,
    NumericPropertyRuleSchema,
    StringPropertyRuleSchema,
    TaxonomicRuleSchema,
    HistoricalRuleSchema,
    TemporalConstraintRuleSchema,
    AndRuleSchema,
    OrRuleSchema,
    NotRuleSchema,
  ])
);

// ─────────────────────────────────────────────────────────────────────────────
// 2.5 Outcome discriminated union
// ─────────────────────────────────────────────────────────────────────────────

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Rules/RuleBase.cs (`OutcomeKindDiscriminators`) */
export const OutcomeKindSchema = z.enum([
  "DepositPointsOutcome",
  "SpendPointsOutcome",
  "ExpirePointsOutcome",
  "TagOutcome",
  "NotificationOutcome",
  "WorkflowOutcome",
  "RuleStateOutcome",
]);
export type OutcomeKind = z.infer<typeof OutcomeKindSchema>;

/**
 * Common fields for every outcome variant.
 * Round-4 observed extras beyond the C# class: `MapToProvider` ships on point outcomes.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/OutcomeBase.cs
 */
const OutcomeBaseFields = {
  Id: z.string(),
  $type: z.string().optional(),
  EventId: z.string().nullable().optional(),
  EventType: z.string().nullable().optional(),
  EventIdProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EventTypeProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  /** Round-4 observed on the wire — not in the C# OutcomeBase but ships for point outcomes. */
  MapToProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
} as const;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/DepositPointsOutcome.cs */
export const DepositPointsOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("DepositPointsOutcome"),
  PointSourceAccountId: z.string().nullable().optional(),
  PointsPerDollar: z.number(),
  EarnDateProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  DollarAmountProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  AffectedPointAccountTypeIds: z.array(z.string()),
});
export type DepositPointsOutcome = z.infer<typeof DepositPointsOutcomeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/SpendPointsOutcome.cs */
export const SpendPointsOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("SpendPointsOutcome"),
  PointSourceAccountId: z.string().nullable().optional(),
  LedgerTypeId: z.string().nullable().optional(),
  WithdrawlAmountProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  AffectedPointAccountTypeIds: z.array(z.string()),
});
export type SpendPointsOutcome = z.infer<typeof SpendPointsOutcomeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/ExpirePointsOutcome.cs */
export const ExpirePointsOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("ExpirePointsOutcome"),
  ExpirationAmount: z.number().nullable().optional(),
  ExpirationPercent: z.number().nullable().optional(),
  ExpirationDateProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EarnDateProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  ExpireAmountProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  AffectedPointAccountTypeIds: z.array(z.string()),
});
export type ExpirePointsOutcome = z.infer<typeof ExpirePointsOutcomeSchema>;

/** @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/TagOutcome.cs */
export const TagOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("TagOutcome"),
  Type: z.string(),
  EntityId: z.string(),
  Name: z.string(),
  ValueProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  Value: z.string(),
  EntityIdProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EffectiveStartDateProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EffectiveStartDate: z.string().datetime({ offset: true }).nullable().optional(),
  EffectiveEndDateProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EffectiveEndDate: z.string().datetime({ offset: true }).nullable().optional(),
  EffectiveStartTimeProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EffectiveStartTime: z.string().nullable().optional(),
  EffectiveEndTimeProvider: z
    .lazy(() => ProviderSchema)
    .nullable()
    .optional(),
  EffectiveEndTime: z.string().nullable().optional(),
  TtlSec: z.number().nullable().optional(),
});
export type TagOutcome = z.infer<typeof TagOutcomeSchema>;

/**
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/NotificationOutcome.cs
 *
 * Server-side STUB — both `AwardOutcomeAsync` + `CalculateOutcomeAsync` return `null`. Schema
 * exists so wire parses don't reject valid JSON; admin UI should not expose editors until the
 * server implements behaviour.
 */
export const NotificationOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("NotificationOutcome"),
});
export type NotificationOutcome = z.infer<typeof NotificationOutcomeSchema>;

/**
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/WorkflowOutcome.cs (server-side stub)
 */
export const WorkflowOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("WorkflowOutcome"),
});
export type WorkflowOutcome = z.infer<typeof WorkflowOutcomeSchema>;

/**
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/RuleStateOutcome.cs (server-side stub)
 */
export const RuleStateOutcomeSchema = z.object({
  ...OutcomeBaseFields,
  Kind: z.literal("RuleStateOutcome"),
});
export type RuleStateOutcome = z.infer<typeof RuleStateOutcomeSchema>;

export type Outcome =
  | DepositPointsOutcome
  | SpendPointsOutcome
  | ExpirePointsOutcome
  | TagOutcome
  | NotificationOutcome
  | WorkflowOutcome
  | RuleStateOutcome;
export const OutcomeSchema: z.ZodType<Outcome> = z.lazy(() =>
  z.discriminatedUnion("Kind", [
    DepositPointsOutcomeSchema,
    SpendPointsOutcomeSchema,
    ExpirePointsOutcomeSchema,
    TagOutcomeSchema,
    NotificationOutcomeSchema,
    WorkflowOutcomeSchema,
    RuleStateOutcomeSchema,
  ])
);

/** `RuleSet.outcomesJsonElement` is array-shaped on the wire. */
export const OutcomeJsonElementSchema = z.array(OutcomeSchema);
export type OutcomeJsonElement = z.infer<typeof OutcomeJsonElementSchema>;

/** `RuleSet.ruleJsonElement` is a single (possibly composite) rule. */
export const RuleJsonElementSchema = RuleSchema;
export type RuleJsonElement = Rule;

/**
 * Client-facing `OutcomeResult` companion to `OutcomeStateBaseSchema` in `loyalty.ts` — the
 * server-emitted result of running an outcome. camelCase per ELP outer envelope.
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Outcomes/OutcomeBase.cs (`OutcomeResult`)
 */
export const OutcomeResultSchema = z.object({
  issuingOutcomeId: z.string(),
  issuingOutcomeKind: OutcomeKindSchema,
  issuingEventId: z.string().optional(),
  issuingEventType: z.string().optional(),
  pointsAwarded: z.number().nullable().optional(),
  pointsRevoked: z.number().nullable().optional(),
  pointAccountTypeId: z.string().nullable().optional(),
  campaignId: z.string().nullable().optional(),
  ruleSetId: z.string().nullable().optional(),
  isAwarded: z.boolean(),
});
export type OutcomeResult = z.infer<typeof OutcomeResultSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// 2.6 Navigation + NavigationInner (Journey raw JSON)
// ─────────────────────────────────────────────────────────────────────────────

/**
 * @see Elevate.ELP/Elevate.ELP.Core/RulesEngine/Journey/SimpleNavigationCriteria.cs
 *
 * Round-4 observed: `NavigationType` ships as **integer** (Cosmos archive) for some campaigns
 * — accept both int and string via `z.union`.
 */
export const NavigationInnerSchema = z.object({
  $type: z.string().optional(),
  Name: z.string().optional(),
  NavigationType: z.union([NavigationTypeSchema, z.number().int(), z.string()]),
  NavConstraint: RuleSchema.nullable().optional(),
  Outcomes: OutcomeJsonElementSchema.nullable().optional(),
});
export type NavigationInner = z.infer<typeof NavigationInnerSchema>;

/**
 * `Journey.navigation` outer wire shape: a dictionary keyed by `NavigationType` literal
 * (`Entry` / `Exit` / `Transition`).
 */
export const NavigationSchema = z.union([
  z.object({
    Entry: NavigationInnerSchema.nullable().optional(),
    Exit: NavigationInnerSchema.nullable().optional(),
    Transition: NavigationInnerSchema.nullable().optional(),
  }),
  z.record(NavigationTypeSchema, NavigationInnerSchema.nullable()),
]);
export type Navigation = z.infer<typeof NavigationSchema>;
