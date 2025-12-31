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

        IQueryable<MessageEvaluationMetric> query = context.MessageEvaluationMetrics
            .AsNoTracking()
            .Include(m => m.Message)
            .ThenInclude(msg => msg!.Game)
            .ThenInclude(g => g!.Player)
            .Include(m => m.Message)
            .ThenInclude(msg => msg!.Game)
            .ThenInclude(g => g!.Scenario)
            .Include(m => m.Message)
            .ThenInclude(msg => msg!.InstructionVersion)
            .Include(m => m.Evaluator);

        // Apply filters
        if (filters != null)
        {
            if (filters.GameId.HasValue)
            {
                query = query.Where(m => m.Message != null && m.Message.GameId == filters.GameId.Value);
            }

            if (!string.IsNullOrEmpty(filters.MetricName))
            {
                query = query.Where(m => m.MetricName.ToLower() == filters.MetricName.ToLower());
            }

            if (filters.MinScore.HasValue)
            {
                query = query.Where(m => m.Score >= filters.MinScore.Value);
            }

            if (filters.MaxScore.HasValue)
            {
                query = query.Where(m => m.Score <= filters.MaxScore.Value);
            }

            if (filters.Passed.HasValue)
            {
                if (filters.Passed.Value)
                {
                    query = query.Where(m => m.Score >= PassThreshold);
                }
                else
                {
                    query = query.Where(m => m.Score < PassThreshold);
                }
            }

            if (filters.InstructionVersionId.HasValue)
            {
                query = query.Where(m => m.Message != null &&
                                         m.Message.InstructionVersionId == filters.InstructionVersionId.Value);
            }

            if (filters.EvaluatorId.HasValue)
            {
                query = query.Where(m => m.EvaluatorId == filters.EvaluatorId.Value);
            }

            if (!string.IsNullOrEmpty(filters.AgentId))
            {
                query = query.Where(m => m.Message != null &&
                                         m.Message.InstructionVersion != null &&
                                         m.Message.InstructionVersion.AgentId == filters.AgentId);
            }
        }

        // Order by game (player name), then message ID for sequential grouping
        query = query.OrderBy(m => m.Message!.Game!.Player!.Name)
            .ThenBy(m => m.MessageId)
            .ThenBy(m => m.MetricName);

        int totalCount = await query.CountAsync(cancellationToken);

        List<MessageEvaluationMetric> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IEnumerable<EvaluationMetricListItemDto> dtos = items.Select(m =>
            new EvaluationMetricListItemDto
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
                GameId = m.Message?.GameId ?? Guid.Empty,
                GamePlayerName = m.Message?.Game?.Player?.Name,
                GameScenarioName = m.Message?.Game?.Scenario?.Name,
                GameRulesetId = m.Message?.Game?.RulesetId,
                AgentVersion = m.Message?.InstructionVersion?.VersionNumber,
                MessagePreview = m.Message?.Text is string text && text.Length > 50
                    ? text[..50] + "..."
                    : m.Message?.Text
            });

        return new EvaluationMetricListResponse
        {
            Items = dtos,
            TotalCount = totalCount,
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
        }).ToList();
    }
}
