namespace MattEland.Jaimes.ServiceLayer.Services;

public class ScenariosService(IDbContextFactory<JaimesDbContext> contextFactory) : IScenariosService
{
    public async Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Scenario[] scenarios = await context.Scenarios
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return scenarios.Select(s => new ScenarioDto
        {
            Id = s.Id,
            RulesetId = s.RulesetId,
            Description = s.Description,
            Name = s.Name,
            ScenarioInstructions = s.ScenarioInstructions,
            InitialGreeting = s.InitialGreeting
        })
            .ToArray();
    }

    public async Task<ScenarioDto> GetScenarioAsync(string id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Scenario? scenario = await context.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (scenario == null) throw new ArgumentException($"Scenario with id '{id}' not found.", nameof(id));

        return new ScenarioDto
        {
            Id = scenario.Id,
            RulesetId = scenario.RulesetId,
            Description = scenario.Description,
            Name = scenario.Name,
            ScenarioInstructions = scenario.ScenarioInstructions,
            InitialGreeting = scenario.InitialGreeting
        };
    }

    public async Task<ScenarioDto> CreateScenarioAsync(string id,
        string rulesetId,
        string? description,
        string name,
        string? initialGreeting,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Check if scenario already exists
        bool exists = await context.Scenarios
            .AnyAsync(s => s.Id == id, cancellationToken);

        if (exists) throw new ArgumentException($"Scenario with id '{id}' already exists.", nameof(id));

        // Verify ruleset exists
        bool rulesetExists = await context.Rulesets
            .AnyAsync(r => r.Id == rulesetId, cancellationToken);

        if (!rulesetExists) throw new ArgumentException($"Ruleset with id '{rulesetId}' not found.", nameof(rulesetId));

        Scenario newScenario = new()
        {
            Id = id,
            RulesetId = rulesetId,
            Description = description,
            Name = name,
            InitialGreeting = initialGreeting
        };

        context.Scenarios.Add(newScenario);
        await context.SaveChangesAsync(cancellationToken);

        return new ScenarioDto
        {
            Id = newScenario.Id,
            RulesetId = newScenario.RulesetId,
            Description = newScenario.Description,
            Name = newScenario.Name,
            ScenarioInstructions = newScenario.ScenarioInstructions,
            InitialGreeting = newScenario.InitialGreeting
        };
    }

    public async Task<ScenarioDto> UpdateScenarioAsync(string id,
        string rulesetId,
        string? description,
        string name,
        string? initialGreeting,
        string? scenarioInstructions,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Scenario? scenario = await context.Scenarios
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (scenario == null) throw new ArgumentException($"Scenario with id '{id}' not found.", nameof(id));

        // Verify ruleset exists
        bool rulesetExists = await context.Rulesets
            .AnyAsync(r => r.Id == rulesetId, cancellationToken);

        if (!rulesetExists) throw new ArgumentException($"Ruleset with id '{rulesetId}' not found.", nameof(rulesetId));

        scenario.RulesetId = rulesetId;
        scenario.Description = description;
        scenario.Name = name;
        scenario.InitialGreeting = initialGreeting;
        scenario.ScenarioInstructions = scenarioInstructions;

        await context.SaveChangesAsync(cancellationToken);

        return new ScenarioDto
        {
            Id = scenario.Id,
            RulesetId = scenario.RulesetId,
            Description = scenario.Description,
            Name = scenario.Name,
            ScenarioInstructions = scenario.ScenarioInstructions,
            InitialGreeting = scenario.InitialGreeting
        };
    }

    public async Task UpdateScenarioInstructionsAsync(string id, string? scenarioInstructions, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Scenario? scenario = await context.Scenarios
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (scenario == null) throw new ArgumentException($"Scenario with id '{id}' not found.", nameof(id));

        scenario.ScenarioInstructions = scenarioInstructions;
        await context.SaveChangesAsync(cancellationToken);
    }
}