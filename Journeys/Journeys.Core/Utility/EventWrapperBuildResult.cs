using Backend.Dto.Structures.Model;

namespace Journeys.Core.Utility;

public sealed class EventWrapperBuildResult
{
    public bool Success { get; init; }

    public ModelDto? Model { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ReferenceWrapperModelId { get; init; }
}
