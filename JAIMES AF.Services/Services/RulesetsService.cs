using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Services.Models;

namespace MattEland.Jaimes.ServiceLayer.Services;

public class RulesetsService(JaimesDbContext context) : IRulesetsService
{
 public async Task<RulesetDto[]> GetRulesetsAsync(CancellationToken cancellationToken = default)
 {
 var rulesets = await context.Rulesets
 .AsNoTracking()
 .ToArrayAsync(cancellationToken);

 return rulesets.Select(r => new RulesetDto
 {
 Id = r.Id,
 Name = r.Name
 }).ToArray();
 }
}
