using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class ScenarioAgentsService(IDbContextFactory<JaimesDbContext> contextFactory) : IScenarioAgentsService
{
    public async Task<ScenarioAgentDto[]> GetScenarioAgentsAsync(string scenarioId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ScenarioAgent[] scenarioAgents = await context.ScenarioAgents
            .AsNoTracking()
            .Where(sa => sa.ScenarioId == scenarioId)
            .ToArrayAsync(cancellationToken);

        return scenarioAgents.ToDto();
    }

    public async Task<ScenarioAgentDto> SetScenarioAgentAsync(string scenarioId, string agentId,
        int? instructionVersionId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Verify scenario exists
        Scenario? scenario = await context.Scenarios.FindAsync([scenarioId], cancellationToken);
        if (scenario == null)
            throw new ArgumentException($"Scenario '{scenarioId}' does not exist.", nameof(scenarioId));

        // Verify agent exists
        Agent? agent = await context.Agents.FindAsync([agentId], cancellationToken);
        if (agent == null)
            throw new ArgumentException($"Agent '{agentId}' does not exist.", nameof(agentId));

        // Verify instruction version exists and belongs to the agent (only if specific version requested)
        if (instructionVersionId.HasValue)
        {
            AgentInstructionVersion? version = await context.AgentInstructionVersions
                .FirstOrDefaultAsync(iv => iv.Id == instructionVersionId && iv.AgentId == agentId, cancellationToken);
            if (version == null)
                throw new ArgumentException(
                    $"Instruction version '{instructionVersionId}' does not exist for agent '{agentId}'.",
                    nameof(instructionVersionId));
        }

        // Get or create scenario agent
        ScenarioAgent? scenarioAgent = await context.ScenarioAgents
            .FirstOrDefaultAsync(sa => sa.ScenarioId == scenarioId && sa.AgentId == agentId, cancellationToken);

        if (scenarioAgent == null)
        {
            scenarioAgent = new ScenarioAgent
            {
                ScenarioId = scenarioId,
                AgentId = agentId,
                InstructionVersionId = instructionVersionId
            };
            context.ScenarioAgents.Add(scenarioAgent);
        }
        else
        {
            scenarioAgent.InstructionVersionId = instructionVersionId;
        }

        await context.SaveChangesAsync(cancellationToken);

        return scenarioAgent.ToDto();
    }

    public async Task RemoveScenarioAgentAsync(string scenarioId, string agentId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ScenarioAgent? scenarioAgent = await context.ScenarioAgents
            .FirstOrDefaultAsync(sa => sa.ScenarioId == scenarioId && sa.AgentId == agentId, cancellationToken);

        if (scenarioAgent == null)
            throw new ArgumentException(
                $"Scenario agent for scenario '{scenarioId}' and agent '{agentId}' does not exist.",
                nameof(scenarioId));

        context.ScenarioAgents.Remove(scenarioAgent);
        await context.SaveChangesAsync(cancellationToken);
    }
}


