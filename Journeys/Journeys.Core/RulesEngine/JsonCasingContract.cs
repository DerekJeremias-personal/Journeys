namespace Journeys.Core.RulesEngine;

public sealed record JsonCasingLayerDto(
    string Id,
    string KeyCasing,
    string? ValueCasing,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<string> Examples);

public sealed record JsonCasingContractDto(
    string Summary,
    IReadOnlyList<JsonCasingLayerDto> Layers,
    IReadOnlyList<string> Exceptions,
    IReadOnlyList<string> AntiPatterns);

public static class JsonCasingContract
{
    public static JsonCasingContractDto Build() => new(
        Summary: "Apply before first validate_campaign: L1 shell camelCase; L2 rule/outcome/provider keys PascalCase; L3 PropertyPath segments and model symbols lowercase; AffectedPointAccountTypeIds values are PAT GUIDs not manifest aliases.",
        Layers:
        [
            new("shell", "camelCase", null,
                ["tenantId", "campaignJson", "name", "status", "events", "journey", "ruleJsonElement", "outcomesJsonElement", "children"],
                ["\"name\": \"Tier earn campaign\"", "\"ruleJsonElement\": { ... }"]),
            new("rulesEngine", "PascalCase", "PascalCase discriminators; PAT ids = GUID strings",
                ["ruleJsonElement", "outcomesJsonElement", "navConstraint", "LeftProvider", "Kind", "$type", "AffectedPointAccountTypeIds"],
                ["\"LeftProvider\": { \"$type\": \"PathValueProvider\" }", "\"AffectedPointAccountTypeIds\": [\"43a4603d-...\"]"]),
            new("symbolsAndPaths", "PascalCase key PropertyPath", "lowercase path segments",
                ["PropertyPath values", "attributes[].symbol", "NaturalKeySymbols", "AccountXIdSymbol"],
                ["\"PropertyPath\": \"event.ordertotal\"", "\"symbol\": \"orderid\""])
        ],
        Exceptions:
        [
            "evaluator.comparison — camelCase key, PascalCase enum value (e.g. GreaterThanOrEqual)",
            "Tier A violation field= in messages may be camelCase — author JSON with PascalCase keys"
        ],
        AntiPatterns:
        [
            "leftProvider / rightProvider / evaluator on rules — use LeftProvider, RightProvider, Evaluator",
            "affectedPointAccountTypeIds on outcomes — use AffectedPointAccountTypeIds (PascalCase) or bind fails silently",
            "AffectedPointAccountTypeIds: [\"spendable\"] — use PAT GUID from manifest item, not alias label",
            "Lowercasing all L2 keys — breaks provider bind (see trace 4b6d3c seq 25–26)"
        ]);
}
