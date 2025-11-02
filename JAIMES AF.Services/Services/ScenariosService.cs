using MattEland.Jaimes.Domain;
using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class ScenariosService(JaimesDbContext context) : IScenariosService
{
    public async Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default)
    {
        Scenario[] scenarios = await context.Scenarios
        .AsNoTracking()
        .ToArrayAsync(cancellationToken);

        return scenarios.Select(s => new ScenarioDto
        {
            Id = s.Id,
            RulesetId = s.RulesetId,
            Description = s.Description,
            Name = s.Name
        }).ToArray();
    }
}
