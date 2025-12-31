using System.Reflection;
using Microsoft.Extensions.AI.Evaluation.Quality;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Implementation of IEvaluatorRegistrar that scans for available evaluators and their metrics.
/// </summary>
public class EvaluatorRegistrar(IDbContextFactory<JaimesDbContext> contextFactory) : IEvaluatorRegistrar
{
    /// <inheritdoc />
    public async Task RegisterEvaluatorsAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Clear existing evaluators to handle the schema change (from metric names to class names)
        // Also clear EvaluatorId in MessageEvaluationMetrics to avoid foreign key issues
        await context.Database.ExecuteSqlRawAsync("UPDATE \"MessageEvaluationMetrics\" SET \"EvaluatorId\" = NULL",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Evaluators\"", cancellationToken);

        // Reset identity if supported (PostgreSQL syntax)
        if (string.Equals(context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Evaluators_Id_seq\" RESTART WITH 1",
                cancellationToken);
        }

        // Scan for IEvaluator implementations
        List<Assembly> assembliesToScan =
            [Assembly.GetExecutingAssembly(), typeof(RelevanceTruthAndCompletenessEvaluator).Assembly];

        // Try to load the worker assembly if it's available (might not be in migration context)
        Assembly? workerAssembly = TryLoadAssembly("MattEland.Jaimes.Workers.AssistantMessageWorker");
        if (workerAssembly != null)
        {
            assembliesToScan.Add(workerAssembly);
        }

        var evaluatorTypes = assembliesToScan
            .Where(a => a != null)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Microsoft.Extensions.AI.Evaluation.IEvaluator).IsAssignableFrom(t) &&
                        !t.IsInterface &&
                        !t.IsAbstract)
            .Distinct()
            .ToList();

        foreach (var type in evaluatorTypes)
        {
            // We register the CLASS name as the Evaluator name
            string name = type.Name;

            // Check if it's already registered (shouldn't happen with the DELETE above but good practice)
            if (await context.Evaluators.AnyAsync(e => e.Name == name, cancellationToken)) continue;

            string description = $"Auto-detected evaluator class: {type.Name}";

            context.Evaluators.Add(new Evaluator
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Assembly? TryLoadAssembly(string name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch
        {
            return null;
        }
    }
}
