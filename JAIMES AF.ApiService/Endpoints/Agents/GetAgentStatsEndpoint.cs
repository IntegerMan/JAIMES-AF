using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

/// <summary>
/// Endpoint for retrieving consolidated statistics for a specific agent or version.
/// </summary>
public class GetAgentStatsEndpoint : EndpointWithoutRequest<AgentStatsResponse>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/admin/agents/{agentId}/stats");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentStatsResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string agentId = Route<string>("agentId") ?? string.Empty;
        int? versionId = Query<int?>("versionId", isRequired: false);

        // Defensive ID resolution: Try exact match first, then fallback to case-insensitive match
        // this helps avoid expensive ToLower calls on the DB when not needed, and ensures we use the "correct" indexed ID.
        string? resolvedId = await DbContext.Agents
            .Where(a => a.Id == agentId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (resolvedId == null)
        {
            resolvedId = await DbContext.Agents
                .Where(a => a.Id.ToLower() == agentId.ToLower())
                .Select(a => a.Id)
                .FirstOrDefaultAsync(ct);
        }

        // Use the route param as fallback if nothing found (might be a new agent or data without an agent record)
        resolvedId ??= agentId;

        // Base filters - matching exactly what is shown in the agent message logs
        IQueryable<Message> messagesQuery = DbContext.Messages
            .Where(m => m.AgentId == resolvedId && !m.IsScriptedMessage);

        // Optional version filtering
        if (versionId.HasValue && versionId.Value > 0)
        {
            messagesQuery = messagesQuery.Where(m => m.InstructionVersionId == versionId);
        }

        // Execute counts directly on the target tables for maximum efficiency and reliability
        int messageCount = await messagesQuery.CountAsync(ct);
        int aiMessageCount = await messagesQuery.CountAsync(m => m.PlayerId == null, ct);
        
        // Joins for feedback, sentiment, and other related data
        var feedbackBase = DbContext.MessageFeedbacks.Where(f => f.Message!.AgentId == resolvedId && !f.Message!.IsScriptedMessage);
        var sentimentBase = DbContext.MessageSentiments.Where(s => s.Message!.AgentId == resolvedId && !s.Message!.IsScriptedMessage);
        var toolBase = DbContext.MessageToolCalls.Where(t => t.Message!.AgentId == resolvedId && !t.Message!.IsScriptedMessage);
        var metricBase = DbContext.MessageEvaluationMetrics.Where(m => m.Message!.AgentId == resolvedId && !m.Message!.IsScriptedMessage);

        if (versionId.HasValue && versionId.Value > 0)
        {
            feedbackBase = feedbackBase.Where(f => f.Message!.InstructionVersionId == versionId);
            sentimentBase = sentimentBase.Where(s => s.Message!.InstructionVersionId == versionId);
            toolBase = toolBase.Where(t => t.Message!.InstructionVersionId == versionId);
            metricBase = metricBase.Where(m => m.Message!.InstructionVersionId == versionId);
        }

        int feedbackPos = await feedbackBase.CountAsync(f => f.IsPositive, ct);
        int feedbackNeg = await feedbackBase.CountAsync(f => !f.IsPositive, ct);
        
        int sentimentPos = await sentimentBase.CountAsync(s => s.Sentiment > 0, ct);
        int sentimentNeu = await sentimentBase.CountAsync(s => s.Sentiment == 0, ct);
        int sentimentNeg = await sentimentBase.CountAsync(s => s.Sentiment < 0, ct);

        int toolCallCount = await toolBase.CountAsync(ct);
        double toolUsageRate = aiMessageCount > 0 ? (double)toolCallCount / aiMessageCount : 0;

        var evaluationMetrics = await metricBase
            .GroupBy(m => new { m.MetricName, m.EvaluatorId })
            .Select(g => new AgentEvaluatorMetricSummaryDto
            {
                EvaluatorId = g.Key.EvaluatorId,
                MetricName = g.Key.MetricName,
                AverageScore = g.Any() ? g.Average(m => m.Score) : 0
            })
            .ToListAsync(ct);

        await Send.OkAsync(new AgentStatsResponse
        {
            MessageCount = messageCount,
            AiMessageCount = aiMessageCount,
            FeedbackPositiveCount = feedbackPos,
            FeedbackNegativeCount = feedbackNeg,
            SentimentPositiveCount = sentimentPos,
            SentimentNeutralCount = sentimentNeu,
            SentimentNegativeCount = sentimentNeg,
            ToolCallCount = toolCallCount,
            ToolUsageRate = toolUsageRate,
            EvaluationMetrics = evaluationMetrics
        }, ct);
    }
}
