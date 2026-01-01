using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class InstructionService(IDbContextFactory<JaimesDbContext> contextFactory) : IInstructionService
{
    public async Task<string?> GetInstructionsAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get the scenario with its agent and instruction version
        var scenarioData = await context.Scenarios
            .AsNoTracking()
            .Where(s => s.Id == scenarioId)
            .Select(s => new
            {
                ScenarioInstructions = s.ScenarioInstructions,
                ScenarioAgents = s.ScenarioAgents
                    .Select(sa => new
                    {
                        sa.InstructionVersionId,
                        InstructionVersion = new
                        {
                            sa.InstructionVersion!.Instructions
                        }
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (scenarioData == null)
            return null;

        // Get the first scenario agent (currently only one agent per scenario)
        var scenarioAgent = scenarioData.ScenarioAgents;
        if (scenarioAgent == null)
            return null;

        string baseInstructions = scenarioAgent.InstructionVersion.Instructions;
        string? scenarioInstructions = scenarioData.ScenarioInstructions;

        // Combine instructions
        if (string.IsNullOrWhiteSpace(scenarioInstructions))
        {
            return baseInstructions;
        }

        return $"{baseInstructions}\n\n---\n\n{scenarioInstructions}";
    }

    public async Task<string?> GetInstructionsForGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get the game with scenario data and overrides
        var gameData = await context.Games
            .AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new
            {
                g.ScenarioId,
                g.AgentId,
                g.InstructionVersionId,
                ScenarioInstructions = g.Scenario!.ScenarioInstructions,
                OverrideInstructions = g.InstructionVersion!.Instructions
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (gameData == null) return null;

        string? systemPrompt;
        if (!string.IsNullOrEmpty(gameData.AgentId) && gameData.InstructionVersionId.HasValue)
        {
            // Use specific agent version if overridden in the game
            systemPrompt = gameData.OverrideInstructions;

            // Still include scenario instructions if any
            if (!string.IsNullOrWhiteSpace(gameData.ScenarioInstructions))
            {
                systemPrompt = $"{systemPrompt}\n\n---\n\n{gameData.ScenarioInstructions}";
            }
        }
        else
        {
            // Fall back to scenario defaults
            systemPrompt = await GetInstructionsAsync(gameData.ScenarioId, cancellationToken);
        }

        return systemPrompt;
    }
}


