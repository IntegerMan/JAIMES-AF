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
        int? versionId = Query<int?>("versionId");

        // Base filters
        IQueryable<Message> messagesQuery = DbContext.Messages.Where(m => m.AgentId == agentId);
        IQueryable<MessageFeedback> feedbackQuery = DbContext.MessageFeedbacks.Where(f => f.Message.AgentId == agentId);
        IQueryable<MessageSentiment> sentimentQuery = DbContext.MessageSentiments.Where(s => s.Message.AgentId == agentId);
        IQueryable<MessageEvaluationMetric> metricsQuery = DbContext.MessageEvaluationMetrics.Where(m => m.Message.AgentId == agentId);
        IQueryable<MessageToolCall> toolsQuery = DbContext.MessageToolCalls.Where(t => t.Message.AgentId == agentId);

        // Version filters
        if (versionId.HasValue)
        {
            messagesQuery = messagesQuery.Where(m => m.InstructionVersionId == versionId);
            feedbackQuery = feedbackQuery.Where(f => f.Message.InstructionVersionId == versionId);
            sentimentQuery = sentimentQuery.Where(s => s.Message.InstructionVersionId == versionId);
            metricsQuery = metricsQuery.Where(m => m.Message.InstructionVersionId == versionId);
            toolsQuery = toolsQuery.Where(t => t.Message.InstructionVersionId == versionId);
        }

        // Execute queries
        int messageCount = await messagesQuery.CountAsync(ct);
        int feedbackPos = await feedbackQuery.CountAsync(f => f.IsPositive, ct);
        int feedbackNeg = await feedbackQuery.CountAsync(f => !f.IsPositive, ct);
        
        int sentimentPos = await sentimentQuery.CountAsync(s => s.Sentiment > 0, ct);
        int sentimentNeu = await sentimentQuery.CountAsync(s => s.Sentiment == 0, ct);
        int sentimentNeg = await sentimentQuery.CountAsync(s => s.Sentiment < 0, ct);

        int toolCallCount = await toolsQuery.CountAsync(ct);

        var evaluationMetrics = await metricsQuery
            .GroupBy(m => m.MetricName)
            .Select(g => new AgentEvaluatorMetricSummaryDto
            {
                MetricName = g.Key,
                AverageScore = g.Any() ? g.Average(m => m.Score) : 0
            })
            .ToListAsync(ct);

        await Send.OkAsync(new AgentStatsResponse
        {
            MessageCount = messageCount,
            FeedbackPositiveCount = feedbackPos,
            FeedbackNegativeCount = feedbackNeg,
            SentimentPositiveCount = sentimentPos,
            SentimentNeutralCount = sentimentNeu,
            SentimentNegativeCount = sentimentNeg,
            ToolCallCount = toolCallCount,
            EvaluationMetrics = evaluationMetrics
        }, ct);
    }
}
