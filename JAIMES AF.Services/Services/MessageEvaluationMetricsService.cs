using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for evaluation metrics operations.
/// </summary>
public class MessageEvaluationMetricsService(IDbContextFactory<JaimesDbContext> contextFactory)
    : IMessageEvaluationMetricsService
{
    private const double PassThreshold = 3.0;

    public async Task<EvaluationMetricListResponse> GetMetricsListAsync(
        int page,
        int pageSize,
        AdminFilterParams? filters = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Fetch all registered evaluators to know what *should* be there
        var registeredEvaluators = await context.Evaluators
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Base query for messages that SHOULD have evaluations
        // Assistant messages that are not scripted
        IQueryable<Message> messageQuery = context.Messages
            .AsNoTracking()
            .Where(m => m.PlayerId == null && !m.IsScriptedMessage);

        // Apply filters to messageQuery
        if (filters != null)
        {
            if (filters.GameId.HasValue)
            {
                messageQuery = messageQuery.Where(m => m.GameId == filters.GameId.Value);
            }

            if (filters.InstructionVersionId.HasValue)
            {
                messageQuery = messageQuery.Where(m => m.InstructionVersionId == filters.InstructionVersionId.Value);
            }

            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                messageQuery = messageQuery.Where(m => m.AgentId == filters.AgentId);
            }

            // Note: Filters on MetricName, Score, and Passed are harder when starting from Messages.
            // We only want to filter the MESSAGE list if we are filtering by Score/Pass status (which implies checking existing metrics).
            // If we are filtering by EvaluatorId or MetricName, we want to Keep all messages so we can show "Missing" rows for them.
            if (filters.MinScore.HasValue || filters.MaxScore.HasValue || filters.Passed.HasValue)
            {
                IQueryable<MessageEvaluationMetric> filteredMetricQuery =
                    context.MessageEvaluationMetrics.AsNoTracking();

                if (!string.IsNullOrEmpty(filters.MetricName))
                {
                    filteredMetricQuery =
                        filteredMetricQuery.Where(m => m.MetricName.ToLower() == filters.MetricName.ToLower());
                }

                if (filters.MinScore.HasValue)
                {
                    filteredMetricQuery = filteredMetricQuery.Where(m => m.Score >= filters.MinScore.Value);
                }

                if (filters.MaxScore.HasValue)
                {
                    filteredMetricQuery = filteredMetricQuery.Where(m => m.Score <= filters.MaxScore.Value);
                }

                if (filters.Passed.HasValue)
                {
                    if (filters.Passed.Value)
                        filteredMetricQuery = filteredMetricQuery.Where(m => m.Score >= PassThreshold);
                    else
                        filteredMetricQuery = filteredMetricQuery.Where(m => m.Score < PassThreshold);
                }

                if (filters.EvaluatorId.HasValue)
                {
                    filteredMetricQuery = filteredMetricQuery.Where(m => m.EvaluatorId == filters.EvaluatorId.Value);
                }

                var filteredMessageIds = filteredMetricQuery.Select(m => m.MessageId).Distinct();
                messageQuery = messageQuery.Where(m => filteredMessageIds.Contains(m.Id));
            }
        }

        // Order by CreatedAt descending to see recent messages first
        messageQuery = messageQuery.OrderByDescending(m => m.CreatedAt);

        int totalMessagesCount = await messageQuery.CountAsync(cancellationToken);

        var pagedMessages = await messageQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Game!)
            .ThenInclude(g => g.Player)
            .Include(m => m.Game!)
            .ThenInclude(g => g.Scenario)
            .Include(m => m.InstructionVersion)
            .ToListAsync(cancellationToken);

        // Fetch metrics for these specific messages
        var messageIds = pagedMessages.Select(m => m.Id).ToList();
        var metricsItems = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.MessageId))
            .Include(m => m.Evaluator)
            .ToListAsync(cancellationToken);

        var dtos = new List<EvaluationMetricListItemDto>();

        foreach (var msg in pagedMessages)
        {
            var msgMetrics = metricsItems.Where(m => m.MessageId == msg.Id).ToList();

            // Create DTOs for existing metrics
            foreach (var m in msgMetrics)
            {
                // If a metric-specific filter is active, skip metrics that don't match
                if (!string.IsNullOrEmpty(filters?.MetricName) &&
                    !m.MetricName.Equals(filters.MetricName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filters?.EvaluatorId.HasValue == true && m.EvaluatorId != filters.EvaluatorId.Value)
                    continue;

                dtos.Add(new EvaluationMetricListItemDto
                {
                    Id = m.Id,
                    MessageId = m.MessageId,
                    MetricName = m.MetricName,
                    Score = m.Score,
                    Passed = m.Score >= PassThreshold,
                    Remarks = m.Remarks,
                    Diagnostics = m.Diagnostics,
                    EvaluatedAt = m.EvaluatedAt,
                    EvaluatorId = m.EvaluatorId,
                    EvaluatorName = m.Evaluator?.Name,
                    GameId = msg.GameId,
                    GamePlayerName = msg.Game?.Player?.Name,
                    GameScenarioName = msg.Game?.Scenario?.Name,
                    GameRulesetId = msg.Game?.RulesetId,
                    AgentVersion = msg.InstructionVersion?.VersionNumber,
                    MessagePreview = msg.Text is string text && text.Length > 50 ? text[..50] + "..." : msg.Text,
                    IsMissing = false
                });
            }

            // Identify missing evaluators for this specific message
            var existingEvaluatorNames = msgMetrics
                .Where(m => m.Evaluator != null)
                .Select(m => m.Evaluator!.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingEvaluators = registeredEvaluators
                .Where(e => !existingEvaluatorNames.Contains(e.Name))
                .ToList();

            foreach (var evaluator in missingEvaluators)
            {
                // If an evaluator filter is active, skip if it doesn't match
                if (filters?.EvaluatorId.HasValue == true && evaluator.Id != filters.EvaluatorId.Value)
                    continue;

                // If a metric filter is active, and it's NOT just the evaluator's name, skip 
                // (Since we don't know the metric name for a missing evaluator easily without more metadata)
                if (!string.IsNullOrEmpty(filters?.MetricName) &&
                    !evaluator.Name.Contains(filters.MetricName, StringComparison.OrdinalIgnoreCase))
                    continue;

                dtos.Add(new EvaluationMetricListItemDto
                {
                    Id = 0,
                    MessageId = msg.Id,
                    MetricName = evaluator.Name,
                    Score = 0,
                    Passed = false,
                    Remarks = "Evaluator has not been run for this message.",
                    EvaluatedAt = DateTime.MinValue,
                    EvaluatorId = evaluator.Id,
                    EvaluatorName = evaluator.Name,
                    GameId = msg.GameId,
                    GamePlayerName = msg.Game?.Player?.Name,
                    GameScenarioName = msg.Game?.Scenario?.Name,
                    GameRulesetId = msg.Game?.RulesetId,
                    AgentVersion = msg.InstructionVersion?.VersionNumber,
                    MessagePreview = msg.Text is string text && text.Length > 50 ? text[..50] + "..." : msg.Text,
                    IsMissing = true
                });
            }
        }

        // Return the count of messages roughly multiplied by evaluator count to approximate the total "rows"
        // This keeps the pagination UI reasonably meaningful.
        int totalRows = totalMessagesCount * (registeredEvaluators.Count > 0 ? registeredEvaluators.Count : 1);

        return new EvaluationMetricListResponse
        {
            Items = dtos,
            TotalCount = totalRows,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<MessageEvaluationMetricResponse>> GetMetricsForMessageAsync(
        int messageId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<MessageEvaluationMetric> metrics = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Include(m => m.Evaluator)
            .Where(m => m.MessageId == messageId)
            .OrderBy(m => m.MetricName)
            .ToListAsync(cancellationToken);

        return metrics.Select(m => new MessageEvaluationMetricResponse
            {
                Id = m.Id,
                MessageId = m.MessageId,
                MetricName = m.MetricName,
                Score = m.Score,
                Remarks = m.Remarks,
                Diagnostics = m.Diagnostics,
                EvaluatedAt = m.EvaluatedAt,
                EvaluationModelId = m.EvaluationModelId,
                EvaluatorId = m.EvaluatorId,
                EvaluatorName = m.Evaluator?.Name
            })
            .ToList();
    }
}
