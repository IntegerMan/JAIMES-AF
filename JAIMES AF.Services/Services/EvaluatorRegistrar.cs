using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI.Evaluation.Quality;
using MattEland.Jaimes.Evaluators;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Implementation of IEvaluatorRegistrar that scans for available evaluators and their metrics.
/// </summary>
public class EvaluatorRegistrar(
    IDbContextFactory<JaimesDbContext> contextFactory) : IEvaluatorRegistrar
{
    /// <inheritdoc />
    public async Task RegisterEvaluatorsAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Note: No longer wiping existing evaluators to preserve IDs and linkages.

        // Scan for IEvaluator implementations in our own assemblies
        List<Assembly> assembliesToScan = [Assembly.GetExecutingAssembly()];

        // Add the evaluators assembly by getting it from a direct type reference
        Assembly evaluatorsAssembly = typeof(PlayerAgencyEvaluator).Assembly;
        assembliesToScan.Add(evaluatorsAssembly);

        var allTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Microsoft.Extensions.AI.Evaluation.IEvaluator).IsAssignableFrom(t) &&
                        !t.IsInterface &&
                        !t.IsAbstract)
            .Concat([typeof(RelevanceTruthAndCompletenessEvaluator)])
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();

        // Fetch existing into a case-insensitive dictionary
        var existingEvaluators = await context.Evaluators
            .ToDictionaryAsync(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var type in allTypes)
        {
            string name = type.Name;

            // Get description from DescriptionAttribute if available
            var descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>();
            string description = descriptionAttribute?.Description ?? $"Auto-detected evaluator class: {type.Name}";

            // Hardcoded description for RTC as it's from an external library
            if (type == typeof(RelevanceTruthAndCompletenessEvaluator))
            {
                description =
                    "Evaluates assistant responses for Relevance to the prompt, Truthfulness relative to context, and Completeness regarding the user's intent.";
            }

            if (!existingEvaluators.TryGetValue(name, out var existingEvaluator))
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
        await ReLinkOrphanedMetricsAsync(context, allTypes, cancellationToken);
    }

    private async Task ReLinkOrphanedMetricsAsync(JaimesDbContext context, List<Type> evaluatorTypes,
        CancellationToken cancellationToken)
    {
        // Fetch all registered evaluators to get their IDs
        var evaluators = await context.Evaluators.ToListAsync(cancellationToken);
        var evaluatorMap = evaluators.ToDictionary(e => e.Name, e => e.Id);

        // Identify which metrics belong to which evaluator classes
        var metricToEvaluatorMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in evaluatorTypes)
        {
            if (evaluatorMap.TryGetValue(type.Name, out int id))
            {
                // Map by known types or naming convention
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

}
