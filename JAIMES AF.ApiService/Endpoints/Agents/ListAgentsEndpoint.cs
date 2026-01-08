using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class ListAgentsEndpoint : Ep.NoReq.Res<AgentListResponse>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/agents");
        AllowAnonymous();
        Description(b => b
            .Produces<AgentListResponse>()
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var agentData = await DbContext.Agents
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Role,
                VersionCount = a.InstructionVersions.Count(),
                FeedbackPos = DbContext.MessageFeedbacks.Count(f =>
                    f.Message!.AgentId == a.Id && !f.Message!.IsScriptedMessage && f.IsPositive),
                FeedbackNeg = DbContext.MessageFeedbacks.Count(f =>
                    f.Message!.AgentId == a.Id && !f.Message!.IsScriptedMessage && !f.IsPositive),
                SentimentPos = DbContext.MessageSentiments.Count(s =>
                    s.Message!.AgentId == a.Id && !s.Message!.IsScriptedMessage && s.Sentiment > 0),
                SentimentNeu = DbContext.MessageSentiments.Count(s =>
                    s.Message!.AgentId == a.Id && !s.Message!.IsScriptedMessage && s.Sentiment == 0),
                SentimentNeg = DbContext.MessageSentiments.Count(s =>
                    s.Message!.AgentId == a.Id && !s.Message!.IsScriptedMessage && s.Sentiment < 0),
                Metrics = DbContext.MessageEvaluationMetrics
                    .Where(m => m.Message!.AgentId == a.Id && !m.Message!.IsScriptedMessage)
                    .GroupBy(m => new { m.MetricName, m.EvaluatorId })
                    .Select(g => new AgentEvaluatorMetricSummaryDto
                    {
                        MetricName = g.Key.MetricName,
                        EvaluatorId = g.Key.EvaluatorId,
                        AverageScore = g.Average(m => m.Score)
                    }).ToList()
            })
            .ToListAsync(ct);

        int totalVersions = await DbContext.AgentInstructionVersions.CountAsync(ct);
        int totalFeedback = await DbContext.MessageFeedbacks.CountAsync(f => !f.Message!.IsScriptedMessage, ct);
        double? avgEval = await DbContext.MessageEvaluationMetrics.AnyAsync(m => !m.Message!.IsScriptedMessage, ct)
            ? await DbContext.MessageEvaluationMetrics.Where(m => !m.Message!.IsScriptedMessage)
                .AverageAsync(m => m.Score, ct)
            : null;

        await Send.OkAsync(new AgentListResponse
        {
            TotalAgents = agentData.Count,
            TotalVersions = totalVersions,
            TotalFeedback = totalFeedback,
            AverageEvaluation = avgEval,
            Agents = agentData.Select(a => new AgentResponse
            {
                Id = a.Id,
                Name = a.Name,
                Role = a.Role,
                VersionCount = a.VersionCount,
                FeedbackPositiveCount = a.FeedbackPos,
                FeedbackNegativeCount = a.FeedbackNeg,
                SentimentPositiveCount = a.SentimentPos,
                SentimentNeutralCount = a.SentimentNeu,
                SentimentNegativeCount = a.SentimentNeg,
                EvaluationMetrics = a.Metrics
            }).ToArray()
        }, ct);
    }
}



