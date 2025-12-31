using System.Reflection;
using Microsoft.Extensions.AI.Evaluation.Quality;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

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

        // Note: No longer wiping existing evaluators to preserve IDs and linkages.

        // Scan for IEvaluator implementations in our own assemblies
        List<Assembly> assembliesToScan = [Assembly.GetExecutingAssembly()];

        // Try to load the worker assembly if it's available
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

        // Manually include the RTC evaluator as it's in an external assembly we're no longer scanning
        evaluatorTypes.Add(typeof(RelevanceTruthAndCompletenessEvaluator));

        foreach (var type in evaluatorTypes)
        {
            // We register the CLASS name as the Evaluator name
            string name = type.Name;

            // Check if it's already registered
            var existingEvaluator =
                await context.Evaluators.FirstOrDefaultAsync(e => e.Name == name, cancellationToken);

            // Get description from DescriptionAttribute if available
            var descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>();
            string description = descriptionAttribute?.Description ?? $"Auto-detected evaluator class: {type.Name}";

            // Hardcoded description for RTC as it's from an external library
            if (type == typeof(RelevanceTruthAndCompletenessEvaluator))
            {
                description =
                    "Evaluates assistant responses for Relevance to the prompt, Truthfulness relative to context, and Completeness regarding the user's intent.";
            }

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
                existingEvaluator.Description = description;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Perform re-linkage for any orphaned metrics
        await ReLinkOrphanedMetricsAsync(context, evaluatorTypes, cancellationToken);
    }

    private async Task ReLinkOrphanedMetricsAsync(JaimesDbContext context, List<Type> evaluatorTypes,
        CancellationToken cancellationToken)
    {
        // Fetch all registered evaluators to get their IDs
        var evaluators = await context.Evaluators.ToListAsync(cancellationToken);
        var evaluatorMap = evaluators.ToDictionary(e => e.Name, e => e.Id);

        // Identify which metrics belong to which evaluator classes
        var metricToEvaluatorMap = new Dictionary<string, int>();
        foreach (var type in evaluatorTypes)
        {
            if (evaluatorMap.TryGetValue(type.Name, out int id))
            {
                // Instantiate the evaluator temporarily to get its metric names
                // This is a bit heavy but ensures accuracy.
                // Alternatively, we could hardcode these or use a convention.
                try
                {
                    // Many evaluators have a parameterless constructor or one that can be mocked
                    // If this fails, we skip it and hope for the best.
                    if (Activator.CreateInstance(type) is Microsoft.Extensions.AI.Evaluation.IEvaluator evaluator)
                    {
                        foreach (var metricName in evaluator.EvaluationMetricNames)
                        {
                            metricToEvaluatorMap[metricName] = id;
                        }
                    }
                }
                catch
                {
                    // Fallback for evaluators without parameterless constructors
                    // We'll try to match by naming convention if nothing else
                    if (type == typeof(RelevanceTruthAndCompletenessEvaluator))
                    {
                        metricToEvaluatorMap["Relevance"] = id;
                        metricToEvaluatorMap["Truth"] = id;
                        metricToEvaluatorMap["Completeness"] = id;
                    }
                    else if (type.Name.EndsWith("Evaluator", StringComparison.Ordinal))
                    {
                        string possibleMetricName = type.Name.Replace("Evaluator", "", StringComparison.Ordinal);
                        if (!metricToEvaluatorMap.ContainsKey(possibleMetricName))
                        {
                            metricToEvaluatorMap[possibleMetricName] = id;
                        }
                    }
                }
            }
        }

        // Update metrics that have no EvaluatorId
        var orphanedMetrics = await context.MessageEvaluationMetrics
            .Where(m => m.EvaluatorId == null)
            .ToListAsync(cancellationToken);

        bool changed = false;
        foreach (var metric in orphanedMetrics)
        {
            // Try exact match first
            if (metricToEvaluatorMap.TryGetValue(metric.MetricName, out int evaluatorId))
            {
                metric.EvaluatorId = evaluatorId;
                changed = true;
            }
            else
            {
                // Try stripping (RTC) suffix or other suffixes
                string strippedName = metric.MetricName;
                int parenIndex = strippedName.IndexOf('(');
                if (parenIndex > 0)
                {
                    strippedName = strippedName[..parenIndex].Trim();
                }

                if (metricToEvaluatorMap.TryGetValue(strippedName, out int strippedEvaluatorId))
                {
                    metric.EvaluatorId = strippedEvaluatorId;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
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
}
