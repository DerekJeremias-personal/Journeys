namespace CampaignContextAudit.Analysis;

public enum DimensionRole { Primary, Secondary }

public sealed record DimensionMapping(int DimensionId, DimensionRole Role);

public static class FindingDimensionMap
{
    public static IReadOnlyList<DimensionMapping> ForCode(string code) =>
        code switch
        {
            "REDISCOVERY_AFTER_CREATION" =>
            [
                new DimensionMapping(4, DimensionRole.Primary),
                new DimensionMapping(2, DimensionRole.Secondary)
            ],
            "REDUNDANT_DISCOVERY" =>
            [
                new DimensionMapping(7, DimensionRole.Primary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "TOOL_RESULT_BLOAT" =>
            [
                new DimensionMapping(6, DimensionRole.Primary),
                new DimensionMapping(8, DimensionRole.Secondary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "WORKFLOW_STATE_DRIFT" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "CREATION_INCOMPLETE_AT_DONE" =>
            [
                new DimensionMapping(1, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "JOURNEY_EMPTY_AT_SUCCESS" =>
            [
                new DimensionMapping(1, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "SUCCESS_NARRATIVE_DRIFT" =>
            [
                new DimensionMapping(5, DimensionRole.Primary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "MCP_INVOCATION_ERROR_CLUSTER" => [new DimensionMapping(5, DimensionRole.Primary)],
            "VALIDATION_UPSERT_LOOP" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(7, DimensionRole.Secondary)
            ],
            "MANIFEST_DRIFT" => [new DimensionMapping(3, DimensionRole.Primary)],
            "FALSE_TOOL_UNAVAILABLE" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "SLOW_TOOL" => [new DimensionMapping(8, DimensionRole.Primary)],
            "LONG_TURN" => [new DimensionMapping(8, DimensionRole.Primary)],
            "SESSION_STALLED_NO_DELIVERY" =>
            [
                new DimensionMapping(5, DimensionRole.Primary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "CREATION_ABORTED" =>
            [
                new DimensionMapping(5, DimensionRole.Primary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "VERIFICATION_DEFERRED" =>
            [
                new DimensionMapping(1, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "VERIFICATION_BLOCKED_NO_ALLOWLIST" =>
            [
                new DimensionMapping(5, DimensionRole.Primary),
                new DimensionMapping(1, DimensionRole.Secondary)
            ],
            "VERIFICATION_MODEL_REBUILD_DRIFT" =>
            [
                new DimensionMapping(4, DimensionRole.Primary),
                new DimensionMapping(7, DimensionRole.Secondary)
            ],
            "EVENT_MODELS_GATE_BLOCKS_MUTATORS" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "PAT_MUTATOR_DEFERRED_GATE_OPEN" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "GATE_CLOSED_PAT_STALL" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Primary),
                new DimensionMapping(4, DimensionRole.Secondary)
            ],
            "PAT_SKIPPED_INLINE_MANIFEST" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "FALSE_VALIDATION_PASS_ZERO_RULESETS" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "JOURNEY_SHAPE_NODES_NOT_CHILDREN" =>
            [
                new DimensionMapping(3, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            "EVENT_PAYLOAD_SCHEMA_LOOP" =>
            [
                new DimensionMapping(4, DimensionRole.Primary),
                new DimensionMapping(7, DimensionRole.Secondary)
            ],
            "LONG_SINGLE_TURN" =>
            [
                new DimensionMapping(8, DimensionRole.Primary),
                new DimensionMapping(5, DimensionRole.Secondary)
            ],
            _ => []
        };

    public static double SeverityPenalty(string severity) => severity.ToLowerInvariant() switch
    {
        "blocking" => 1.50,
        "intent-breaking" => 1.25,
        "degrading" => 0.75,
        "wasteful" => 0.35,
        _ => 0.0
    };
}
