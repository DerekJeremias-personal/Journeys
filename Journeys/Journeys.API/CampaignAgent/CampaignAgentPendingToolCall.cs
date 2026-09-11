namespace Journeys.API.CampaignAgent;

internal readonly record struct CampaignAgentPendingToolCall(string ToolName, string ArgsJson, long StartTimestamp);
