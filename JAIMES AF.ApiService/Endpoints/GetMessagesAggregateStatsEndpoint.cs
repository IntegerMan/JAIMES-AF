using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting aggregate evaluation statistics for a filtered set of messages.
/// </summary>
public class GetMessagesAggregateStatsEndpoint : EndpointWithoutRequest<MessagesAggregateStatsResponse>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/admin/messages/aggregate-stats");
        AllowAnonymous();
        Description(b => b
            .Produces<MessagesAggregateStatsResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? agentId = Query<string?>("agentId", false);
        int? versionId = Query<int?>("versionId", false);
        Guid? gameId = Query<Guid?>("gameId", false);

        // Build base message query
        IQueryable<Message> messagesQuery = DbContext.Messages
            .Where(m => !m.IsScriptedMessage);

        // Apply optional filters
        if (!string.IsNullOrEmpty(agentId))
        {
            messagesQuery = messagesQuery.Where(m => m.AgentId == agentId);
        }

        if (versionId.HasValue && versionId.Value > 0)
        {
            messagesQuery = messagesQuery.Where(m => m.InstructionVersionId == versionId);
        }

        if (gameId.HasValue)
        {
            messagesQuery = messagesQuery.Where(m => m.GameId == gameId.Value);
        }

        // Get counts
        int messageCount = await messagesQuery.CountAsync(ct);
        int aiMessageCount = await messagesQuery.CountAsync(m => m.PlayerId == null, ct);

        // Build metrics query with same filters
        IQueryable<Repositories.Entities.MessageEvaluationMetric> metricBase = DbContext.MessageEvaluationMetrics
            .Where(m => !m.Message!.IsScriptedMessage);

        if (!string.IsNullOrEmpty(agentId))
        {
            metricBase = metricBase.Where(m => m.Message!.AgentId == agentId);
        }

        if (versionId.HasValue && versionId.Value > 0)
        {
            metricBase = metricBase.Where(m => m.Message!.InstructionVersionId == versionId);
        }

        if (gameId.HasValue)
        {
            metricBase = metricBase.Where(m => m.Message!.GameId == gameId.Value);
        }

        // Aggregate metrics by evaluator
        var evaluationMetrics = await metricBase
            .GroupBy(m => new { m.MetricName, m.EvaluatorId })
            .Select(g => new AgentEvaluatorMetricSummaryDto
            {
                EvaluatorId = g.Key.EvaluatorId,
                MetricName = g.Key.MetricName,
                AverageScore = g.Any() ? g.Average(m => m.Score) : 0
            })
            .ToListAsync(ct);

        await Send.OkAsync(new MessagesAggregateStatsResponse
        {
            MessageCount = messageCount,
            AiMessageCount = aiMessageCount,
            EvaluationMetrics = evaluationMetrics
        }, ct);
    }
}
