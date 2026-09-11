using CampaignContextAudit.Analysis;
using CampaignContextAudit.Budget;
using CampaignContextAudit.Mapping;
using CampaignContextAudit.Transcript;
using Journeys.Core.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class SessionBudgetEstimatorTests
{
    private static string ConversionSamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AgentMessageSamples", "conversion.json"));

    [Fact]
    public void Conversion_fixture_has_positive_session_chars()
    {
        var loaded = TranscriptLoader.Load(ConversionSamplePath);
        var workflowRow = loaded.WorkflowRows.OrderByDescending(w => w.Sequence).First();
        var state = CampaignWorkflowState.FromWorkflowRow(AgentMessageDocMapper.ToAgentMessage(workflowRow));
        Assert.NotNull(state);

        var history = loaded.ChatRows.OrderBy(r => r.Sequence).ToList();
        var sessionChars = SessionBudgetEstimator.EstimateSessionChars(state, history);

        Assert.True(sessionChars > 0);
    }
}
