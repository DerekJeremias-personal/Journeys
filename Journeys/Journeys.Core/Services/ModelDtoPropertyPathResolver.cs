using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;

namespace Journeys.Core.Services;

/// <summary>
/// Resolves a campaign rule <c>event.*</c> path against a Backend <see cref="ModelDto"/> graph.
/// </summary>
internal static class ModelDtoPropertyPathResolver
{
    /// <summary>
    /// Returns whether the terminal attribute is <c>ModelTaxonomy</c> and the path prefix suitable for <see cref="RulesEngine.Rules.TaxonomicRule"/> collection binding (all segments except the leaf).
    /// </summary>
    public static async Task<(bool IsTaxonomyLeaf, string? CollectionPrefixPath)> TryResolveAsync(
        string propertyPath,
        ModelDto eventModel,
        Func<string, string, CancellationToken, Task<ModelDto?>> loadModelAsync,
        CancellationToken cancellationToken)
    {
        var segments = propertyPath.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return (false, null);

        if (!segments[0].Equals("event", StringComparison.OrdinalIgnoreCase))
            return (false, null);

        ModelDto current = eventModel;
        for (var i = 1; i < segments.Length; i++)
        {
            var symbol = segments[i];
            var attr = current.Attributes?.FirstOrDefault(a =>
                string.Equals(a.Symbol, symbol, StringComparison.Ordinal));

            if (attr == null)
                return (false, null);

            var isLast = i == segments.Length - 1;
            if (isLast)
            {
                var isTax = string.Equals(attr.Type, "ModelTaxonomy", StringComparison.Ordinal);
                var prefix = string.Join('.', segments.Take(segments.Length - 1));
                return (isTax, prefix);
            }

            if (attr is ModelAttributeListDto list)
            {
                var child = await loadModelAsync(list.ModelId, list.ModelType, cancellationToken).ConfigureAwait(false);
                if (child == null)
                    return (false, null);
                current = child;
                continue;
            }

            if (attr is ModelAttributeObjectDto obj)
            {
                var child = await loadModelAsync(obj.ModelId, obj.ModelType, cancellationToken).ConfigureAwait(false);
                if (child == null)
                    return (false, null);
                current = child;
                continue;
            }

            return (false, null);
        }

        return (false, null);
    }
}
