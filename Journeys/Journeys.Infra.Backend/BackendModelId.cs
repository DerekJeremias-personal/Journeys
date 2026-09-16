namespace Journeys.Infra.Backend;

public static class BackendModelId
{
    public const string LoyaltyModelType = "loyalty";

    public static bool IsUsable(string? modelId) =>
        !string.IsNullOrWhiteSpace(modelId)
        && !modelId.Trim().Equals("unknown", StringComparison.OrdinalIgnoreCase);

    public static string Require(string? modelId, string paramName = "modelId")
    {
        if (!IsUsable(modelId))
        {
            throw new ArgumentException(
                "A real Backend model id is required; 'unknown' is not sent.",
                paramName);
        }

        return modelId!.Trim();
    }
}
