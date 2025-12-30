using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories; // For JaimesDbContext
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetMessageEvaluationMetricsEndpoint : EndpointWithoutRequest<IEnumerable<MessageEvaluationMetricResponse>>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/messages/{messageId}/metrics");
        AllowAnonymous();
        Description(b => b
            .Produces<IEnumerable<MessageEvaluationMetricResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Messages"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? messageIdStr = Route<string>("messageId", true);
        if (string.IsNullOrEmpty(messageIdStr) || !int.TryParse(messageIdStr, out int messageId))
        {
            ThrowError("Invalid message ID format");
            return;
        }

        // Check if message exists
        bool messageExists = await DbContext.Messages.AnyAsync(m => m.Id == messageId, ct);
        if (!messageExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var metrics = await DbContext.MessageEvaluationMetrics
            .Where(m => m.MessageId == messageId)
            .Select(m => new MessageEvaluationMetricResponse
            {
                Id = m.Id,
                MessageId = m.MessageId,
                MetricName = m.MetricName,
                Score = m.Score,
                Remarks = m.Remarks,
                Diagnostics = m.Diagnostics,
                EvaluatedAt = m.EvaluatedAt,
                EvaluationModelId = m.EvaluationModelId
            })
            .ToListAsync(ct);

        await Send.OkAsync(metrics, ct);
    }
}
