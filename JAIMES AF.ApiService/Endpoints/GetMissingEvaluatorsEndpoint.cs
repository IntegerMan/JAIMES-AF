using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to get missing evaluators for a message.
/// </summary>
public static class GetMissingEvaluatorsEndpoint
{
    public static void MapGetMissingEvaluatorsEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/messages/{messageId}/missing-evaluators",
                async Task<Results<Ok<MissingEvaluatorsResponse>, NotFound>> (
                    int messageId,
                    IDbContextFactory<JaimesDbContext> contextFactory,
                    CancellationToken cancellationToken) =>
                {
                    await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

                    // Load the message
                    var message = await context.Messages
                        .AsNoTracking()
                        .Where(m => m.Id == messageId)
                        .Select(m => new { m.Id, m.IsScriptedMessage, m.PlayerId })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (message == null)
                    {
                        return TypedResults.NotFound();
                    }

                    // Get all registered evaluators
                    var registeredEvaluators = await context.Evaluators
                        .AsNoTracking()
                        .Select(e => e.Name)
                        .ToListAsync(cancellationToken);

                    // Get existing metrics for this message (grouped by evaluator)
                    var existingEvaluatorIds = await context.MessageEvaluationMetrics
                        .AsNoTracking()
                        .Where(m => m.MessageId == messageId && m.EvaluatorId != null)
                        .Select(m => m.EvaluatorId!.Value)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    // Find which evaluators ran by looking up their IDs
                    var existingEvaluatorNames = await context.Evaluators
                        .AsNoTracking()
                        .Where(e => existingEvaluatorIds.Contains(e.Id))
                        .Select(e => e.Name)
                        .ToListAsync(cancellationToken);

                    // Calculate missing evaluators
                    var missingEvaluators = registeredEvaluators
                        .Except(existingEvaluatorNames, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // Check eligibility: must be assistant message (PlayerId == null) and not scripted
                    bool isEligible = message.PlayerId == null && !message.IsScriptedMessage;

                    var response = new MissingEvaluatorsResponse
                    {
                        MessageId = messageId,
                        MissingEvaluators = missingEvaluators,
                        TotalRegisteredEvaluators = registeredEvaluators.Count,
                        ExistingMetricsCount = existingEvaluatorNames.Count,
                        IsEligibleForEvaluation = isEligible
                    };

                    return TypedResults.Ok(response);
                })
            .WithName("GetMissingEvaluators")
            .WithTags("Messages");
    }
}
