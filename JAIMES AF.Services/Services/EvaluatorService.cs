using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for retrieving evaluator data and statistics.
/// </summary>
public class EvaluatorService(IDbContextFactory<JaimesDbContext> contextFactory) : IEvaluatorService
{
    /// <inheritdoc />
    public async Task<EvaluatorListResponse> GetEvaluatorsAsync(
        int page,
        int pageSize,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get all evaluators
        var evaluators = await context.Evaluators
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        // Build query for metrics with optional filters
        IQueryable<Repositories.Entities.MessageEvaluationMetric> metricsQuery = context.MessageEvaluationMetrics
            .AsNoTracking()
            .Include(m => m.Message)
            .ThenInclude(msg => msg!.InstructionVersion);

        // Apply optional filters
        if (instructionVersionId.HasValue)
        {
            metricsQuery = metricsQuery.Where(m =>
                m.Message != null && m.Message.InstructionVersionId == instructionVersionId.Value);
        }
        else if (!string.IsNullOrEmpty(agentId))
        {
            metricsQuery = metricsQuery.Where(m =>
                m.Message != null &&
                m.Message.InstructionVersion != null &&
                m.Message.InstructionVersion.AgentId == agentId);
        }

        if (gameId.HasValue)
        {
            metricsQuery = metricsQuery.Where(m => m.Message != null && m.Message.GameId == gameId.Value);
        }

        // Get metrics grouped by evaluator
        var metricsWithEvaluators = await metricsQuery
            .Where(m => m.EvaluatorId != null)
            .Select(m => new
            {
                m.EvaluatorId,
                m.MetricName,
                m.Score
            })
            .ToListAsync(cancellationToken);

        // Group by evaluator to compute statistics
        var statsByEvaluatorId = metricsWithEvaluators
            .GroupBy(m => m.EvaluatorId)
            .ToDictionary(
                g => g.Key!.Value,
                g => new
                {
                    MetricCount = g.Count(),
                    AverageScore = g.Average(m => m.Score),
                    PassCount = g.Count(m => m.Score >= 3),
                    FailCount = g.Count(m => m.Score < 3)
                });

        // Also group by metric name for evaluators without EvaluatorId
        var statsByMetricName = metricsWithEvaluators
            .GroupBy(m => m.MetricName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    MetricCount = g.Count(),
                    AverageScore = g.Average(m => m.Score),
                    PassCount = g.Count(m => m.Score >= 3),
                    FailCount = g.Count(m => m.Score < 3)
                },
                StringComparer.OrdinalIgnoreCase);

        // Map evaluators to DTOs with statistics
        List<EvaluatorItemDto> allItems = evaluators
            .Select(e =>
            {
                // Try to get stats by EvaluatorId first, then fall back to metric name
                bool hasStatsById = statsByEvaluatorId.TryGetValue(e.Id, out var statsById);
                bool hasStatsByName = statsByMetricName.TryGetValue(e.Name, out var statsByName);

                var stats = hasStatsById ? statsById : (hasStatsByName ? statsByName : null);

                return new EvaluatorItemDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Description = e.Description,
                    CreatedAt = e.CreatedAt,
                    MetricCount = stats?.MetricCount ?? 0,
                    AverageScore = stats?.AverageScore,
                    PassCount = stats?.PassCount ?? 0,
                    FailCount = stats?.FailCount ?? 0
                };
            })
            .OrderByDescending(e => e.MetricCount)
            .ThenBy(e => e.Name)
            .ToList();

        int totalCount = allItems.Count;

        // Apply pagination
        List<EvaluatorItemDto> items = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new EvaluatorListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
