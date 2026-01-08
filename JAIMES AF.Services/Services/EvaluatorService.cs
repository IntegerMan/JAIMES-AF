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
            .Where(m => !m.Message!.IsScriptedMessage)
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

        // Map evaluators to DTOs with statistics
        List<EvaluatorItemDto> allItems = evaluators
            .Select(e =>
            {
                statsByEvaluatorId.TryGetValue(e.Id, out var stats);

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

    /// <inheritdoc />
    public async Task<EvaluatorItemDto?> GetEvaluatorByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var evaluator = await context.Evaluators
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (evaluator == null) return null;

        // Fetch statistics for this evaluator
        var stats = await context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => m.EvaluatorId == id && !m.Message!.IsScriptedMessage)
            .GroupBy(m => m.EvaluatorId)
            .Select(g => new
            {
                MetricCount = g.Count(),
                AverageScore = g.Average(m => m.Score),
                PassCount = g.Count(m => m.Score >= 3),
                FailCount = g.Count(m => m.Score < 3)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new EvaluatorItemDto
        {
            Id = evaluator.Id,
            Name = evaluator.Name,
            Description = evaluator.Description,
            CreatedAt = evaluator.CreatedAt,
            MetricCount = stats?.MetricCount ?? 0,
            AverageScore = stats?.AverageScore,
            PassCount = stats?.PassCount ?? 0,
            FailCount = stats?.FailCount ?? 0
        };
    }

    /// <inheritdoc />
    public async Task<EvaluatorStatsResponse?> GetEvaluatorStatsAsync(
        int evaluatorId,
        string? agentId = null,
        int? instructionVersionId = null,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var evaluator = await context.Evaluators
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == evaluatorId, cancellationToken);

        if (evaluator == null) return null;

        // Build query for metrics with filters
        IQueryable<Repositories.Entities.MessageEvaluationMetric> metricsQuery = context.MessageEvaluationMetrics
            .AsNoTracking()
            .Where(m => m.EvaluatorId == evaluatorId && !m.Message!.IsScriptedMessage)
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

        // Fetch all scores to compute statistics and distribution
        var scores = await metricsQuery
            .Select(m => m.Score)
            .ToListAsync(cancellationToken);

        if (scores.Count == 0)
        {
            return new EvaluatorStatsResponse
            {
                EvaluatorId = evaluator.Id,
                Name = evaluator.Name,
                Description = evaluator.Description,
                CreatedAt = evaluator.CreatedAt,
                TotalCount = 0,
                AverageScore = null,
                PassCount = 0,
                FailCount = 0,
                ScoreDistribution = new ScoreDistribution()
            };
        }

        // Compute statistics
        int totalCount = scores.Count;
        double averageScore = scores.Average();
        int passCount = scores.Count(s => s >= 3);
        int failCount = scores.Count(s => s < 3);

        // Compute score distribution (buckets: 1 = [1,2), 2 = [2,3), 3 = [3,4), 4 = [4,5), 5 = [5,5])
        var distribution = new ScoreDistribution
        {
            Score1 = scores.Count(s => s >= 1 && s < 2),
            Score2 = scores.Count(s => s >= 2 && s < 3),
            Score3 = scores.Count(s => s >= 3 && s < 4),
            Score4 = scores.Count(s => s >= 4 && s < 5),
            Score5 = scores.Count(s => s >= 5)
        };

        return new EvaluatorStatsResponse
        {
            EvaluatorId = evaluator.Id,
            Name = evaluator.Name,
            Description = evaluator.Description,
            CreatedAt = evaluator.CreatedAt,
            TotalCount = totalCount,
            AverageScore = averageScore,
            PassCount = passCount,
            FailCount = failCount,
            ScoreDistribution = distribution
        };
    }
}
