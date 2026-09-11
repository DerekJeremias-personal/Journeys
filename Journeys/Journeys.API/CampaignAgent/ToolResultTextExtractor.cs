using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

internal static class ToolResultTextExtractor
{
    public static bool TryExtractText(object? result, out string text, out Func<string, object?> rewrap)
    {
        switch (result)
        {
            case string s:
                text = s; rewrap = static d => d; return true;
            case JsonElement je:
                text = je.ValueKind == JsonValueKind.String ? je.GetString() ?? string.Empty : je.GetRawText();
                rewrap = static d => d; return true;
            case TextContent tc:
                text = tc.Text ?? string.Empty; rewrap = static d => new TextContent(d); return true;
            case IEnumerable<AIContent> contents:
            {
                var texts = contents.OfType<TextContent>().ToList();
                if (texts.Count == 0) { text = string.Empty; rewrap = static d => d; return false; }
                text = string.Concat(texts.Select(t => t.Text));
                rewrap = d => new List<AIContent> { new TextContent(d) };
                return true;
            }
            default:
                text = string.Empty; rewrap = static d => d; return false;
        }
    }
}
