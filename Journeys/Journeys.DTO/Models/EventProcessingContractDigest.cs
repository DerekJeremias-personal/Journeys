namespace Journeys.DTO.Models;

public sealed class EventProcessingContractDigest
{
    public int SchemaVersion { get; set; } = 1;
    public string EventModelId { get; set; } = string.Empty;
    public string? EventModelName { get; set; }
    public string? EventModelType { get; set; }
    public string? WrapperModelId { get; set; }
    public AccountLinkDigest AccountLink { get; set; } = new();
    public NaturalKeyDigest NaturalKey { get; set; } = new();
    public string? TimeOfOccurrenceSymbol { get; set; }
    public string? ModelTag { get; set; }
    public bool IsProcessEventEligible { get; set; }
    public bool IsLoyaltyAccountCreationEvent { get; set; }
    public string ProcessingRole { get; set; } = "standard_event";
    public string? ProcessingRoleDescription { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class AccountLinkDigest
{
    public string? SymbolPath { get; set; }
    public string Role { get; set; } = "loyalty_external_id";
    public string Description { get; set; } =
        "Path on ProcessEvent root JSON used for GetLoyaltyAccountByExtIdAsync.";
}

public sealed class NaturalKeyDigest
{
    public List<string> Symbols { get; set; } = new();
    public string JoinWith { get; set; } = "|";
    public string Role { get; set; } = "idempotency_key";
    public string Description { get; set; } =
        "Payload values joined for occurrence identity — not the loyalty account link.";
}
