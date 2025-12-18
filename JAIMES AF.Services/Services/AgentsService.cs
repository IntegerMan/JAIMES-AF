using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class AgentsService(IDbContextFactory<JaimesDbContext> contextFactory) : IAgentsService
{
    public async Task<AgentDto[]> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Agent[] agents = await context.Agents
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return agents.ToDto();
    }

    public async Task<AgentDto?> GetAgentAsync(string id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Agent? agent = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return agent?.ToDto();
    }

    public async Task<AgentDto> CreateAgentAsync(string name, string role, string instructions, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        string id = name.ToLowerInvariant().Replace(" ", "");
        
        // Check if agent already exists
        bool exists = await context.Agents
            .AnyAsync(a => a.Id == id, cancellationToken);
        
        if (exists) throw new ArgumentException($"Agent with id '{id}' already exists.", nameof(name));
        
        Agent agent = new()
        {
            Id = id,
            Name = name,
            Role = role
        };

        context.Agents.Add(agent);
        
        // Always create an initial instruction version when creating an agent
        AgentInstructionVersion initialVersion = new()
        {
            AgentId = id,
            VersionNumber = "v1.0",
            Instructions = instructions,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        context.AgentInstructionVersions.Add(initialVersion);
        await context.SaveChangesAsync(cancellationToken);

        return agent.ToDto();
    }

    public async Task<AgentDto> UpdateAgentAsync(string id, string name, string role, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Agent? agent = await context.Agents.FindAsync([id], cancellationToken);
        if (agent == null)
            throw new ArgumentException($"Agent '{id}' does not exist.", nameof(id));

        agent.Name = name;
        agent.Role = role;

        await context.SaveChangesAsync(cancellationToken);

        return agent.ToDto();
    }

    public async Task DeleteAgentAsync(string id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Agent? agent = await context.Agents.FindAsync([id], cancellationToken);
        if (agent == null)
            throw new ArgumentException($"Agent '{id}' does not exist.", nameof(id));

        // Delete associated instruction versions first (DeleteBehavior.Restrict prevents deletion otherwise)
        AgentInstructionVersion[] instructionVersions = await context.AgentInstructionVersions
            .Where(iv => iv.AgentId == id)
            .ToArrayAsync(cancellationToken);
        context.AgentInstructionVersions.RemoveRange(instructionVersions);

        // Delete any scenario agent associations (DeleteBehavior.Restrict prevents deletion otherwise)
        ScenarioAgent[] scenarioAgents = await context.ScenarioAgents
            .Where(sa => sa.AgentId == id)
            .ToArrayAsync(cancellationToken);
        context.ScenarioAgents.RemoveRange(scenarioAgents);

        // Now safe to delete the agent
        context.Agents.Remove(agent);
        await context.SaveChangesAsync(cancellationToken);
    }
}
