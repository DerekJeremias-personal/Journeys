using System;
using System.Collections.Generic;

namespace Journeys.Core.Utility;

/// <summary>
/// Inputs that decide which models keep full schema when a catalog tool result is digested.
/// Eventable models (campaign events) retain attributes; known static platform models stay catalog lines.
/// </summary>
public sealed record ModelCatalogRetainSpec(
    IReadOnlyCollection<string> CatalogOnlyModelNames,
    IReadOnlyCollection<string> RetainModelIds)
{
    /// <summary>
    /// Platform models with static equivalents in code/governance — catalog line only; call get_model when needed.
    /// </summary>
    public static readonly IReadOnlyCollection<string> DefaultCatalogOnlyModelNames =
        new[] { "Campaign", "PointAccountType" };

    public static ModelCatalogRetainSpec Empty { get; } =
        new(DefaultCatalogOnlyModelNames, Array.Empty<string>());
}

/// <summary>Outcome of a catalog digest, including sizes for logging.</summary>
public sealed record ModelCatalogDigestResult(
    string Json,
    bool Transformed,
    int OriginalChars,
    int DigestChars,
    int ModelCount,
    int RetainedCount);
