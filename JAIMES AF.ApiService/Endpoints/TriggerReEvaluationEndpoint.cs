using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint to trigger re-evaluation for missing evaluators on a message.
/// </summary>
public static class TriggerReEvaluationEndpoint
{
    public static void MapTriggerReEvaluationEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/messages/{messageId}/reevaluate",
                async Task<Results<Accepted, NotFound, BadRequest<string>>> (
                    int messageId,
                    IDbContextFactory<JaimesDbContext> contextFactory,
                    IMessagePublisher messagePublisher,
                    CancellationToken cancellationToken) =>
                {
                    await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

                    // Load the message
                    var message = await context.Messages
                        .AsNoTracking()
                        .Where(m => m.Id == messageId)
                        .Select(m => new { m.Id, m.GameId, m.IsScriptedMessage, m.PlayerId })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (message == null)
                    {
                        return TypedResults.NotFound();
                    }

                    // Validate eligibility: must be assistant message and not scripted
                    if (message.PlayerId != null)
                    {
                        return TypedResults.BadRequest("Only assistant messages can be evaluated.");
                    }

                    if (message.IsScriptedMessage)
                    {
                        return TypedResults.BadRequest("Scripted messages are not eligible for evaluation.");
                    }

                    // Get missing evaluators
                    var registeredEvaluators = await context.Evaluators
                        .AsNoTracking()
                        .Select(e => e.Name)
                        .ToListAsync(cancellationToken);

                    var existingEvaluatorIds = await context.MessageEvaluationMetrics
                        .AsNoTracking()
                        .Where(m => m.MessageId == messageId && m.EvaluatorId != null)
                        .Select(m => m.EvaluatorId!.Value)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    var existingEvaluatorNames = await context.Evaluators
                        .AsNoTracking()
                        .Where(e => existingEvaluatorIds.Contains(e.Id))
                        .Select(e => e.Name)
                        .ToListAsync(cancellationToken);

                    var missingEvaluators = registeredEvaluators
                        .Except(existingEvaluatorNames, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (missingEvaluators.Count == 0)
                    {
                        return TypedResults.BadRequest("No missing evaluators for this message.");
                    }

                    // Publish message for re-evaluation
                    var queueMessage = new ConversationMessageQueuedMessage
                    {
                        MessageId = message.Id,
                        GameId = message.GameId,
                        Role = ChatRole.Assistant,
                        EvaluateMissingOnly = true,
                        EvaluatorsToRun = missingEvaluators
                    };

                    await messagePublisher.PublishAsync(queueMessage, cancellationToken);

                    return TypedResults.Accepted((string?)null);
                })
            .WithName("TriggerReEvaluation")
            .WithTags("Messages");
    }
}
