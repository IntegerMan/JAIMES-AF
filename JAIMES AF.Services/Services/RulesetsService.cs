namespace MattEland.Jaimes.ServiceLayer.Services;

public class RulesetsService(IDbContextFactory<JaimesDbContext> contextFactory) : IRulesetsService
{
    public async Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Ruleset[] rulesets = await context.Rulesets
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return rulesets.ToDto();
    }

    public async Task<RulesetDto> GetRulesetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Ruleset? ruleset = await context.Rulesets
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (ruleset == null) throw new ArgumentException($"Ruleset with id '{id}' not found.", nameof(id));

        return ruleset.ToDto();
    }

    public async Task<RulesetDto> CreateRulesetAsync(string id,
        string name,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Check if ruleset already exists
        bool exists = await context.Rulesets
            .AnyAsync(r => r.Id == id, cancellationToken);

        if (exists) throw new ArgumentException($"Ruleset with id '{id}' already exists.", nameof(id));

        Ruleset newRuleset = new()
        {
            Id = id,
            Name = name
        };

        context.Rulesets.Add(newRuleset);
        await context.SaveChangesAsync(cancellationToken);

        return newRuleset.ToDto();
    }

    public async Task<RulesetDto> UpdateRulesetAsync(string id,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Ruleset? ruleset = await context.Rulesets
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (ruleset == null) throw new ArgumentException($"Ruleset with id '{id}' not found.", nameof(id));

        ruleset.Name = name;
        ruleset.Description = description;

        await context.SaveChangesAsync(cancellationToken);

        return ruleset.ToDto();
    }

    public async Task<bool> TryCreateRulesetAsync(string id,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Check if ruleset already exists
        bool exists = await context.Rulesets
            .AnyAsync(r => r.Id == id, cancellationToken);

        if (exists) return false;

        Ruleset newRuleset = new()
        {
            Id = id,
            Name = name,
            Description = description
        };

        context.Rulesets.Add(newRuleset);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Handle race condition: another caller created the ruleset between our check and save.
            // This is expected behavior for TryCreate - we return false indicating we didn't create it.
            return false;
        }
    }
}