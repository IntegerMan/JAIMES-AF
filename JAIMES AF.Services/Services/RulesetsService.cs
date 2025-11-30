using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class RulesetsService(JaimesDbContext context) : IRulesetsService
{
    public async Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default)
    {
        Ruleset[] rulesets = await context.Rulesets
        .AsNoTracking()
        .ToArrayAsync(cancellationToken);

        return rulesets.ToDto();
    }

    public async Task<RulesetDto> GetRulesetAsync(string id, CancellationToken cancellationToken = default)
    {
        Ruleset? ruleset = await context.Rulesets
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (ruleset == null)
        {
            throw new ArgumentException($"Ruleset with id '{id}' not found.", nameof(id));
        }

        return ruleset.ToDto();
    }

    public async Task<RulesetDto> CreateRulesetAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        // Check if ruleset already exists
        bool exists = await context.Rulesets
            .AnyAsync(r => r.Id == id, cancellationToken);

        if (exists)
        {
            throw new ArgumentException($"Ruleset with id '{id}' already exists.", nameof(id));
        }

        Ruleset newRuleset = new()
        {
            Id = id,
            Name = name
        };

        context.Rulesets.Add(newRuleset);
        await context.SaveChangesAsync(cancellationToken);

        return newRuleset.ToDto();
    }

    public async Task<RulesetDto> UpdateRulesetAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        Ruleset? ruleset = await context.Rulesets
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (ruleset == null)
        {
            throw new ArgumentException($"Ruleset with id '{id}' not found.", nameof(id));
        }

        ruleset.Name = name;

        await context.SaveChangesAsync(cancellationToken);

        return ruleset.ToDto();
    }
}
