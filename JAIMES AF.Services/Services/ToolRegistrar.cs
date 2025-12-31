using System.Reflection;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Implementation of IToolRegistrar that scans the tools assembly for available tools.
/// </summary>
public class ToolRegistrar(IDbContextFactory<JaimesDbContext> contextFactory) : IToolRegistrar
{
    /// <inheritdoc />
    public async Task RegisterToolsAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Scan JAIMES AF.Tools assembly for tools
        Assembly toolsAssembly = typeof(PlayerInfoTool).Assembly;

        var toolMethods = toolsAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null)
            .ToList();

        foreach (var method in toolMethods)
        {
            var descriptionAttr = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!;

            // Extract tool name from method name (e.g., "SearchRulesAsync" -> "SearchRules")
            string name = method.Name;
            if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^5];
            }

            string description = descriptionAttr.Description;

            // Try to figure out category from namespace or class name
            string? category = method.DeclaringType?.Name.Replace("Tool", "");

            // Check if tool already exists
            Tool? existingTool = await context.Tools
                .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower(), cancellationToken);

            if (existingTool == null)
            {
                context.Tools.Add(new Tool
                {
                    Name = name,
                    Description = description,
                    Category = category,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Update description and category if they changed
                existingTool.Description = description;
                existingTool.Category = category;
                context.Tools.Update(existingTool);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
