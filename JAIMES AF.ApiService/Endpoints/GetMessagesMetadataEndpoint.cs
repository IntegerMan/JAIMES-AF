using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;
using FastEndpoints;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetMessagesMetadataEndpoint : Endpoint<MessagesMetadataRequest, MessagesMetadataResponse>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Post("/messages/metadata");
        AllowAnonymous();
        Description(b => b
            .Produces<MessagesMetadataResponse>(StatusCodes.Status200OK)
            .WithTags("Messages"));
    }

    public override async Task HandleAsync(MessagesMetadataRequest req, CancellationToken ct)
    {
        if (req.MessageIds.Count == 0)
        {
            await Send.OkAsync(new MessagesMetadataResponse(), ct);
            return;
        }

        // 1. Fetch Feedback
        var feedbackDict = await DbContext.MessageFeedbacks
            .Where(f => req.MessageIds.Contains(f.MessageId))
            .ToDictionaryAsync(f => f.MessageId, f => new MessageFeedbackResponse
            {
                Id = f.Id,
                MessageId = f.MessageId,
                IsPositive = f.IsPositive,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                InstructionVersionId = f.InstructionVersionId
            }, ct);

        // 2. Fetch Tool Calls
        var toolCallsList = await DbContext.MessageToolCalls
            .Where(tc => req.MessageIds.Contains(tc.MessageId))
            .Select(tc => new MessageToolCallResponse
            {
                Id = tc.Id,
                MessageId = tc.MessageId,
                ToolName = tc.ToolName,
                InputJson = tc.InputJson,
                OutputJson = tc.OutputJson,
                CreatedAt = tc.CreatedAt,
                InstructionVersionId = tc.InstructionVersionId
            })
            .ToListAsync(ct);

        var toolCallsDict = toolCallsList
            .GroupBy(tc => tc.MessageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 3. Fetch Evaluation Metrics
        var metricsList = await DbContext.MessageEvaluationMetrics
            .Where(m => req.MessageIds.Contains(m.MessageId))
            .Select(m => new MessageEvaluationMetricResponse
            {
                Id = m.Id,
                MessageId = m.MessageId,
                MetricName = m.MetricName,
                Score = m.Score,
                Remarks = m.Remarks,
                EvaluatedAt = m.EvaluatedAt,
                EvaluationModelId = m.EvaluationModelId
            })
            .ToListAsync(ct);

        var metricsDict = metricsList
            .GroupBy(m => m.MessageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. Fetch Message Sentiment (from Messages table directly)
        // 4. Fetch Message Sentiment (from Messages table via MessageSentiment navigation)
        var sentimentDict = await DbContext.Messages
            .Where(m => req.MessageIds.Contains(m.Id) && m.MessageSentiment != null)
            .Include(m => m.MessageSentiment)
            .Select(m => new
            {
                m.Id,
                Sentiment = m.MessageSentiment!.Sentiment,
                m.MessageSentiment.Confidence,
                SentimentSource =
                    (int?)m.MessageSentiment
                        .SentimentSource // Cast to nullable int if needed or access property directly
            })
            .ToDictionaryAsync(m => m.Id, m => new MessageSentimentResponse
            {
                Sentiment = m.Sentiment,
                Confidence = m.Confidence,
                SentimentSource = m.SentimentSource
            }, ct);

        // Build Response
        await Send.OkAsync(new MessagesMetadataResponse
        {
            Feedback = feedbackDict,
            ToolCalls = toolCallsDict,
            Metrics = metricsDict,
            Sentiment = sentimentDict
        }, ct);
    }
}
