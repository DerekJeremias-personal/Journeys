namespace Journeys.API.A2a;

internal static class JourneysA2aAgentCardBuilder
{
    internal static AgentCardDto Build(string publicBaseUrl)
    {
        var baseTrim = publicBaseUrl.TrimEnd('/');
        return new AgentCardDto
        {
            Name = "Journeys Agent",
            Description = "A2A access to Journeys API campaigns, journeys, accounts, events, and ingestion. Tool execution aligns with Journeys MCP tools (see Journeys-MCP-Contract.md). Send JSON-RPC SendMessage with a data part: {\"tool\":\"<McpToolName>\",\"arguments\":{...}}.",
            Url = $"{baseTrim}/",
            Version = "1.0.0",
            DocumentationUrl = "https://a2a-protocol.org/latest/specification/",
            Provider = new Dictionary<string, string>
            {
                ["organization"] = "Journeys",
                ["url"] = baseTrim
            },
            DefaultInputModes = new List<string> { "application/json", "text/plain" },
            DefaultOutputModes = new List<string> { "application/json" },
            Capabilities = new AgentCapabilitiesDto
            {
                Streaming = false,
                PushNotifications = false,
                StateTransitionHistory = false,
                ExtendedAgentCard = false
            },
            SupportedInterfaces = new List<SupportedInterfaceDto>
            {
                new()
                {
                    Url = $"{baseTrim}/a2a/rpc",
                    ProtocolBinding = "JSONRPC",
                    ProtocolVersion = "1.0"
                }
            },
            Skills = new List<AgentSkillDto>
            {
                new()
                {
                    Id = "Journeys-campaigns",
                    Name = "Campaigns and journeys",
                    Description = "Read/list campaigns, segments, journeys, stats; UpsertCampaign and DeleteCampaign. MCP tool names: GetCampaign, ListCampaigns, GetJourneyFromCampaign, ...",
                    Tags = new List<string> { "campaigns", "loyalty", "journeys" }
                },
                new()
                {
                    Id = "Journeys-accounts-events",
                    Name = "Accounts, tiers, events, ingestion",
                    Description = "Accounts, point types, tier preview/move, ProcessEvent, ingestion folders. See Journeys-MCP-Contract.md.",
                    Tags = new List<string> { "accounts", "events", "tiers", "ingestion" }
                }
            }
        };
    }
}
