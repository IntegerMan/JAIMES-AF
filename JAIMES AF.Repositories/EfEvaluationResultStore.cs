using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.AI.Evaluation.Reporting;

namespace MattEland.Jaimes.Repositories;

/// <summary>
/// An implementation of <see cref="IEvaluationResultStore"/> that stores results in a PostgreSQL database via EF Core.
/// </summary>
public class EfEvaluationResultStore(IDbContextFactory<JaimesDbContext> dbContextFactory) : IEvaluationResultStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc/>
    public async IAsyncEnumerable<ScenarioRunResult> ReadResultsAsync(
        string? executionName = null,
        string? scenarioName = null,
        string? iterationName = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = dbContextFactory.CreateDbContext();

        IQueryable<EvaluationScenarioIteration> query = context.EvaluationScenarioIterations
            .Include(si => si.Execution)
            .AsNoTracking();

        if (executionName != null)
        {
            query = query.Where(si => si.ExecutionName == executionName);
        }

        if (scenarioName != null)
        {
            query = query.Where(si => si.ScenarioName == scenarioName);
        }

        if (iterationName != null)
        {
            query = query.Where(si => si.IterationName == iterationName);
        }

        // Order results by execution timestamp first, then by scenario name, and finally by iteration name.
        query = query.OrderBy(si => si.Execution!.CreatedAt)
            .ThenBy(si => si.ScenarioName)
            .ThenBy(si => si.IterationName);

        await foreach (EvaluationScenarioIteration iteration in query.AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            ScenarioRunResult? result =
                JsonSerializer.Deserialize<ScenarioRunResult>(iteration.ResultJson, JsonOptions);

            yield return result ?? throw new JsonException(
                $"Failed to deserialize result for execution '{iteration.ExecutionName}', scenario '{iteration.ScenarioName}', iteration '{iteration.IterationName}'");
        }
    }

    /// <inheritdoc/>
    public async ValueTask WriteResultsAsync(
        IEnumerable<ScenarioRunResult> results,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using JaimesDbContext context = dbContextFactory.CreateDbContext();

            foreach (ScenarioRunResult result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Ensure execution exists
                EvaluationExecution? execution = await context.EvaluationExecutions
                    .FindAsync([result.ExecutionName], cancellationToken);

                if (execution == null)
                {
                    execution = new EvaluationExecution
                    {
                        ExecutionName = result.ExecutionName,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.EvaluationExecutions.Add(execution);
                }

                string resultJson = JsonSerializer.Serialize(result, JsonOptions);

                EvaluationScenarioIteration? iteration = await context.EvaluationScenarioIterations
                    .FindAsync([result.ExecutionName, result.ScenarioName, result.IterationName], cancellationToken);

                if (iteration == null)
                {
                    iteration = new EvaluationScenarioIteration
                    {
                        ExecutionName = result.ExecutionName,
                        ScenarioName = result.ScenarioName,
                        IterationName = result.IterationName,
                        ResultJson = resultJson
                    };
                    context.EvaluationScenarioIterations.Add(iteration);
                }
                else
                {
                    iteration.ResultJson = resultJson;
                    context.Entry(iteration).State = EntityState.Modified;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteResultsAsync(
        string? executionName = null,
        string? scenarioName = null,
        string? iterationName = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using JaimesDbContext context = dbContextFactory.CreateDbContext();

            IQueryable<EvaluationScenarioIteration> query = context.EvaluationScenarioIterations;

            if (executionName != null)
            {
                query = query.Where(si => si.ExecutionName == executionName);
            }

            if (scenarioName != null)
            {
                query = query.Where(si => si.ScenarioName == scenarioName);
            }

            if (iterationName != null)
            {
                query = query.Where(si => si.IterationName == iterationName);
            }

            context.EvaluationScenarioIterations.RemoveRange(query);
            await context.SaveChangesAsync(cancellationToken);

            // Delete any executions that no longer have any associated results
            List<EvaluationExecution> orphanedExecutions = await context.EvaluationExecutions
                .Where(e => !e.ScenarioIterations.Any())
                .ToListAsync(cancellationToken);

            if (orphanedExecutions.Count > 0)
            {
                context.EvaluationExecutions.RemoveRange(orphanedExecutions);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetLatestExecutionNamesAsync(
        int? count = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = dbContextFactory.CreateDbContext();

        IQueryable<string> query = context.EvaluationExecutions
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.ExecutionName);

        if (count.HasValue)
        {
            query = query.Take(count.Value);
        }

        await foreach (string name in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return name;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetScenarioNamesAsync(
        string executionName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = dbContextFactory.CreateDbContext();

        IQueryable<string> query = context.EvaluationScenarioIterations
            .Where(si => si.ExecutionName == executionName)
            .Select(si => si.ScenarioName)
            .Distinct()
            .OrderBy(name => name);

        await foreach (string name in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return name;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetIterationNamesAsync(
        string executionName,
        string scenarioName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = dbContextFactory.CreateDbContext();

        IQueryable<string> query = context.EvaluationScenarioIterations
            .Where(si => si.ExecutionName == executionName && si.ScenarioName == scenarioName)
            .Select(si => si.IterationName)
            .Distinct()
            .OrderBy(name => name);

        await foreach (string name in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return name;
        }
    }
}
