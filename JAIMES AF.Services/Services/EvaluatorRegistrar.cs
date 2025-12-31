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

        // Scan for IEvaluator implementations
        // We'll look for AssistantMessageWorker assembly, or any assembly containing IEvaluators
        List<Assembly> assembliesToScan = [Assembly.GetExecutingAssembly()];
        Assembly? workerAssembly = TryLoadAssembly("MattEland.Jaimes.Workers.AssistantMessageWorker");
        if (workerAssembly != null)
        {
            assembliesToScan.Add(workerAssembly);
        }

        var evaluatorTypes = assembliesToScan
            .Where(a => a != null)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Microsoft.Extensions.AI.Evaluation.IEvaluator).IsAssignableFrom(t) && !t.IsInterface &&
                        !t.IsAbstract)
            .ToList();

        // Specific handling for built-in evaluators that might not be in the scanned assemblies
        List<Microsoft.Extensions.AI.Evaluation.IEvaluator> builtInEvaluators =
        [
            new RelevanceTruthAndCompletenessEvaluator()
        ];

        foreach (var builtIn in builtInEvaluators)
        {
            await RegisterMetricsAsync(context, builtIn.GetType(), builtIn.EvaluationMetricNames, cancellationToken);
        }

        foreach (var type in evaluatorTypes)
        {
            // Avoid double-registering if we already did it for built-in
            if (builtInEvaluators.Any(be => be.GetType() == type)) continue;

            // Create an instance or use reflection to get evaluation metric names
            var instance = TryCreateInstance(type);
            IEnumerable<string> metricNames;

            if (instance is Microsoft.Extensions.AI.Evaluation.IEvaluator evaluator)
            {
                metricNames = evaluator.EvaluationMetricNames;
            }
            else
            {
                // Fallback to property check via reflection if instantiation fails
                var property =
                    type.GetProperty(nameof(Microsoft.Extensions.AI.Evaluation.IEvaluator.EvaluationMetricNames));
                if (property != null)
                {
                    metricNames = (IEnumerable<string>)property.GetValue(null)!;
                }
                else
                {
                    continue;
                }
            }

            await RegisterMetricsAsync(context, type, metricNames, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RegisterMetricsAsync(JaimesDbContext context, Type type, IEnumerable<string> metricNames,
        CancellationToken cancellationToken)
    {
        foreach (var name in metricNames)
        {
            // Check if evaluator already exists
            Evaluator? existingEvaluator = await context.Evaluators
                .FirstOrDefaultAsync(e => e.Name.ToLower() == name.ToLower(), cancellationToken);

            string description = $"Auto-detected metric from {type.Name}";

            if (existingEvaluator == null)
            {
                context.Evaluators.Add(new Evaluator
                {
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Update description if it's the auto-generated one or empty
                if (string.IsNullOrEmpty(existingEvaluator.Description) ||
                    existingEvaluator.Description.StartsWith("Auto-detected", StringComparison.OrdinalIgnoreCase))
                {
                    existingEvaluator.Description = description;
                    context.Evaluators.Update(existingEvaluator);
                }
            }
        }
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

    private static object? TryCreateInstance(Type type)
    {
        try
        {
            // Try parameterless constructor first
            return Activator.CreateInstance(type);
        }
        catch
        {
            // Many evaluators have constructors with parameters, so this might fail.
            // In a real scenario, we might need a more sophisticated approach or a marker attribute.
            return null;
        }
    }
}
