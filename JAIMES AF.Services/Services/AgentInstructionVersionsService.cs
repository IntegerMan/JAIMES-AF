using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class AgentInstructionVersionsService(IDbContextFactory<JaimesDbContext> contextFactory) : IAgentInstructionVersionsService
{
    public async Task<AgentInstructionVersionDto[]> GetInstructionVersionsAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        AgentInstructionVersion[] versions = await context.AgentInstructionVersions
            .AsNoTracking()
            .Where(iv => iv.AgentId == agentId)
            .OrderByDescending(iv => iv.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return versions.ToDto();
    }

    public async Task<AgentInstructionVersionDto?> GetInstructionVersionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        AgentInstructionVersion? version = await context.AgentInstructionVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(iv => iv.Id == id, cancellationToken);

        return version?.ToDto();
    }

    public async Task<AgentInstructionVersionDto> CreateInstructionVersionAsync(string agentId, string versionNumber, string instructions, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Verify agent exists
        Agent? agent = await context.Agents.FindAsync([agentId], cancellationToken);
        if (agent == null)
            throw new ArgumentException($"Agent '{agentId}' does not exist.", nameof(agentId));

        // Check if version already exists
        bool versionExists = await context.AgentInstructionVersions
            .AnyAsync(iv => iv.AgentId == agentId && iv.VersionNumber == versionNumber, cancellationToken);
        if (versionExists)
            throw new ArgumentException($"Version '{versionNumber}' already exists for agent '{agentId}'.", nameof(versionNumber));

        AgentInstructionVersion version = new()
        {
            AgentId = agentId,
            VersionNumber = versionNumber,
            Instructions = instructions,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.AgentInstructionVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken);

        return version.ToDto();
    }

    public async Task<AgentInstructionVersionDto> UpdateInstructionVersionAsync(int id, string versionNumber, string instructions, bool? isActive, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        AgentInstructionVersion? version = await context.AgentInstructionVersions.FindAsync([id], cancellationToken);
        if (version == null)
            throw new ArgumentException($"Instruction version '{id}' does not exist.", nameof(id));

        // Check if version number conflicts with another version
        if (version.VersionNumber != versionNumber)
        {
            bool versionExists = await context.AgentInstructionVersions
                .AnyAsync(iv => iv.AgentId == version.AgentId && iv.VersionNumber == versionNumber && iv.Id != id, cancellationToken);
            if (versionExists)
                throw new ArgumentException($"Version '{versionNumber}' already exists for agent '{version.AgentId}'.", nameof(versionNumber));
        }

        version.VersionNumber = versionNumber;
        version.Instructions = instructions;
        if (isActive.HasValue)
            version.IsActive = isActive.Value;

        await context.SaveChangesAsync(cancellationToken);

        return version.ToDto();
    }

    public async Task<AgentInstructionVersionDto?> GetActiveInstructionVersionAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        AgentInstructionVersion? version = await context.AgentInstructionVersions
            .AsNoTracking()
            .Where(iv => iv.AgentId == agentId && iv.IsActive)
            .OrderByDescending(iv => iv.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return version?.ToDto();
    }
}
