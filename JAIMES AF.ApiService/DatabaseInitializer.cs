using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService;

public class DatabaseInitializer(ActivitySource activitySource, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(WebApplication app)
    {
        using Activity? activity = activitySource.StartActivity("DatabaseInitialization", ActivityKind.Internal);
        activity?.SetTag("component", "database-init");

        bool skipDbInit = app.Configuration.GetValue<bool>("SkipDatabaseInitialization");
        activity?.SetTag("db.init.skip_config", skipDbInit);

        if (skipDbInit)
        {
            logger?.LogInformation(
                "Database initialization skipped via configuration (SkipDatabaseInitialization=true).");
            activity?.SetTag("db.initialization.skipped", true);
        }
        else
        {
            logger?.LogInformation("Database initialization running now.");

            using Activity? migrateActivity = activitySource.StartActivity("ApplyMigrations", ActivityKind.Internal);
            migrateActivity?.SetTag("db.operation", "migrate");

            try
            {
                // Use the centralized database initialization method
                await app.InitializeDatabaseAsync();
                migrateActivity?.SetTag("db.migrate.success", true);

                // Seed default agents and instruction versions
                await SeedDefaultAgentsAsync(app);
            }
            catch (Exception ex)
            {
                migrateActivity?.SetTag("db.migrate.success", false);
                migrateActivity?.SetTag("error", true);
                migrateActivity?.SetTag("error.message", ex.Message);
                logger?.LogError(ex, "Database initialization failed.");
                throw;
            }
        }
    }

    private async Task SeedDefaultAgentsAsync(WebApplication app)
    {
        using Activity? seedActivity = activitySource.StartActivity("SeedDefaultAgents", ActivityKind.Internal);
        seedActivity?.SetTag("db.operation", "seed-agents");

        try
        {
            // Get DbContext directly to ensure we can create agents with specific IDs
            using var scope = app.Services.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<JaimesDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync(CancellationToken.None);

            // First, clean up any duplicate agents (keep the one with the expected ID, delete others)
            await CleanupDuplicateAgentsAsync(context);
            
            // Save cleanup changes before proceeding with seeding
            await context.SaveChangesAsync(CancellationToken.None);

            // Create default agents if they don't exist
            var defaultAgents = new[]
            {
                ("defaultGameMaster", "Default Game Master", "GameMaster"),
                ("narrator", "Story Narrator", "Narrator"),
                ("dungeonMaster", "Dungeon Master", "GameMaster")
            };

            foreach (var (id, name, role) in defaultAgents)
            {
                try
                {
                    // Check if agent exists by name (to avoid duplicates with different IDs)
                    var existingAgent = await context.Agents
                        .FirstOrDefaultAsync(a => a.Name == name, CancellationToken.None);
                    
                    if (existingAgent == null)
                    {
                        // Check if agent exists with the expected ID (in case it was created with a different name)
                        var existingById = await context.Agents.FindAsync([id], CancellationToken.None);
                        
                        if (existingById == null)
                        {
                            // Validate ID is not null or empty
                            if (string.IsNullOrWhiteSpace(id))
                            {
                                logger?.LogError("Cannot create agent with null or empty ID for name: {AgentName}", name);
                                continue;
                            }

                            // Create the agent with the specific ID
                            Agent agent = new()
                            {
                                Id = id,
                                Name = name,
                                Role = role
                            };
                            context.Agents.Add(agent);
                            logger?.LogInformation("Created default agent: {AgentId} ({AgentName})", agent.Id, agent.Name);

                            // Create default instruction version (EF Core will handle the relationship)
                            var instructions = GetDefaultInstructionsForAgent(id);
                            AgentInstructionVersion version = new()
                            {
                                AgentId = id, // Explicitly set the foreign key
                                VersionNumber = "v1.0",
                                Instructions = instructions,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            context.AgentInstructionVersions.Add(version);
                            logger?.LogInformation("Created default instruction version for agent: {AgentId}", id);
                        }
                        else
                        {
                            // Agent exists with expected ID but different name - update it
                            existingById.Name = name;
                            existingById.Role = role;
                            logger?.LogInformation("Updated existing agent {AgentId} to match expected name: {AgentName}", id, name);
                            
                            // Ensure it has an active instruction version
                            bool hasActiveVersion = await context.AgentInstructionVersions
                                .AnyAsync(iv => iv.AgentId == id && iv.IsActive, CancellationToken.None);
                            
                            if (!hasActiveVersion)
                            {
                                var instructions = GetDefaultInstructionsForAgent(id);
                                AgentInstructionVersion version = new()
                                {
                                    AgentId = id,
                                    VersionNumber = "v1.0",
                                    Instructions = instructions,
                                    CreatedAt = DateTime.UtcNow,
                                    IsActive = true
                                };
                                context.AgentInstructionVersions.Add(version);
                                logger?.LogInformation("Created missing instruction version for existing agent: {AgentId}", id);
                            }
                        }
                    }
                    else
                    {
                        // Agent exists with this name - ensure it has the correct ID and active instruction version
                        if (existingAgent.Id != id)
                        {
                            // Agent exists but with different ID - update the ID if possible, or log a warning
                            logger?.LogWarning("Agent with name '{AgentName}' exists with ID '{ExistingId}' but expected ID '{ExpectedId}'. Skipping creation to avoid duplicates.", 
                                name, existingAgent.Id, id);
                        }
                        
                        // Ensure it has an active instruction version
                        bool hasActiveVersion = await context.AgentInstructionVersions
                            .AnyAsync(iv => iv.AgentId == existingAgent.Id && iv.IsActive, CancellationToken.None);
                        
                        if (!hasActiveVersion)
                        {
                            // Validate agent ID is not null or empty
                            if (string.IsNullOrWhiteSpace(existingAgent.Id))
                            {
                                logger?.LogError("Cannot create instruction version for agent with null or empty ID: {AgentName}", name);
                                continue;
                            }

                            // Use the agent's actual ID, not the expected one
                            var instructions = GetDefaultInstructionsForAgent(id);
                            AgentInstructionVersion version = new()
                            {
                                AgentId = existingAgent.Id,
                                VersionNumber = "v1.0",
                                Instructions = instructions,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            context.AgentInstructionVersions.Add(version);
                            logger?.LogInformation("Created missing instruction version for existing agent: {AgentId}", existingAgent.Id);
                        }
                        else
                        {
                            logger?.LogInformation("Agent {AgentId} ({AgentName}) already has active instruction version", existingAgent.Id, name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to create default agent {AgentId}", id);
                }
            }

            // Save all changes at once
            await context.SaveChangesAsync(CancellationToken.None);
            seedActivity?.SetTag("db.seed.success", true);
        }
        catch (Exception ex)
        {
            seedActivity?.SetTag("db.seed.success", false);
            seedActivity?.SetTag("error", true);
            seedActivity?.SetTag("error.message", ex.Message);
            logger?.LogError(ex, "Failed to seed default agents.");
            // Don't throw - seeding failure shouldn't prevent app startup
        }
    }

    private async Task CleanupDuplicateAgentsAsync(JaimesDbContext context)
    {
        var defaultAgentNames = new[] { "Default Game Master", "Story Narrator", "Dungeon Master" };
        var expectedIds = new Dictionary<string, string>
        {
            { "Default Game Master", "defaultGameMaster" },
            { "Story Narrator", "narrator" },
            { "Dungeon Master", "dungeonMaster" }
        };

        foreach (var agentName in defaultAgentNames)
        {
            var agentsWithSameName = await context.Agents
                .Where(a => a.Name == agentName)
                .ToListAsync(CancellationToken.None);

            if (agentsWithSameName.Count > 1)
            {
                var expectedId = expectedIds[agentName];
                var agentWithExpectedId = agentsWithSameName.FirstOrDefault(a => a.Id == expectedId);
                
                // If we have an agent with the expected ID, keep it and delete all others
                // Otherwise, keep the first one and delete the rest (the seeding logic will create the correct one)
                var agentToKeep = agentWithExpectedId ?? agentsWithSameName.First();

                logger?.LogInformation("Found {Count} duplicate agents with name '{Name}'. Keeping agent with ID '{KeepId}', removing others.",
                    agentsWithSameName.Count, agentName, agentToKeep.Id);

                // Delete the duplicates (but not the one we're keeping)
                foreach (var duplicate in agentsWithSameName)
                {
                    if (duplicate.Id != agentToKeep.Id)
                    {
                        // Also delete associated instruction versions
                        var instructionVersions = await context.AgentInstructionVersions
                            .Where(iv => iv.AgentId == duplicate.Id)
                            .ToListAsync(CancellationToken.None);
                        context.AgentInstructionVersions.RemoveRange(instructionVersions);

                        // Delete any scenario agent associations
                        var scenarioAgents = await context.ScenarioAgents
                            .Where(sa => sa.AgentId == duplicate.Id)
                            .ToListAsync(CancellationToken.None);
                        context.ScenarioAgents.RemoveRange(scenarioAgents);

                        context.Agents.Remove(duplicate);
                        logger?.LogInformation("Removed duplicate agent: {AgentId} ({AgentName})", duplicate.Id, duplicate.Name);
                    }
                }

                // If the kept agent doesn't have the expected ID, delete it too so the seeding logic can create the correct one
                if (agentToKeep.Id != expectedId)
                {
                    logger?.LogInformation("Agent '{Name}' has ID '{ActualId}' but expected ID '{ExpectedId}'. Removing it so correct one can be created.",
                        agentName, agentToKeep.Id, expectedId);
                    
                    // Delete associated instruction versions
                    var instructionVersions = await context.AgentInstructionVersions
                        .Where(iv => iv.AgentId == agentToKeep.Id)
                        .ToListAsync(CancellationToken.None);
                    context.AgentInstructionVersions.RemoveRange(instructionVersions);

                    // Delete any scenario agent associations
                    var scenarioAgents = await context.ScenarioAgents
                        .Where(sa => sa.AgentId == agentToKeep.Id)
                        .ToListAsync(CancellationToken.None);
                    context.ScenarioAgents.RemoveRange(scenarioAgents);

                    context.Agents.Remove(agentToKeep);
                }
            }
            else if (agentsWithSameName.Count == 1)
            {
                // Single agent exists - check if it has the correct ID
                var expectedId = expectedIds[agentName];
                var existingAgent = agentsWithSameName.First();
                
                if (existingAgent.Id != expectedId)
                {
                    logger?.LogInformation("Agent '{Name}' has ID '{ActualId}' but expected ID '{ExpectedId}'. Removing it so correct one can be created.",
                        agentName, existingAgent.Id, expectedId);
                    
                    // Delete associated instruction versions
                    var instructionVersions = await context.AgentInstructionVersions
                        .Where(iv => iv.AgentId == existingAgent.Id)
                        .ToListAsync(CancellationToken.None);
                    context.AgentInstructionVersions.RemoveRange(instructionVersions);

                    // Delete any scenario agent associations
                    var scenarioAgents = await context.ScenarioAgents
                        .Where(sa => sa.AgentId == existingAgent.Id)
                        .ToListAsync(CancellationToken.None);
                    context.ScenarioAgents.RemoveRange(scenarioAgents);

                    context.Agents.Remove(existingAgent);
                }
            }
        }
    }

    private static string GetDefaultInstructionsForAgent(string agentId) => agentId switch
    {
        "defaultGameMaster" => "You are an experienced Dungeons & Dragons Dungeon Master. Guide players through engaging adventures using D&D 5th Edition rules. Be creative, fair, and entertaining. Keep responses concise but descriptive. Always ask 'What do you do?' after describing situations. Use D&D mechanics appropriately for combat and skill checks.",
        "narrator" => "You are a skilled storyteller and narrator. Paint vivid pictures with your words, describe scenes in detail, and maintain narrative flow. Focus on atmosphere, character development, and engaging descriptions. Keep responses focused on storytelling rather than game mechanics.",
        "dungeonMaster" => "You are a classic Dungeons & Dragons Dungeon Master. Run combat encounters, adjudicate rules, create challenges, and manage NPC interactions. Be impartial, creative with encounters, and ensure balanced gameplay. Use D&D 5th Edition rules strictly and provide tactical combat descriptions.",
        _ => "You are a helpful assistant."
    };
}