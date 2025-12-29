namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class GameMapper
{
    [UserMapping]
    public static GameDto ToDto(this Game game)
    {
        MessageDto[]? messages = game.Messages?
            .OrderBy(m => m.Id)
            .Select(m => m.ToDto())
            .ToArray();
        
        DateTime? lastPlayedAt = messages?.Length > 0
            ? messages.Max(m => m.CreatedAt)
            : null;

        return new GameDto
        {
            GameId = game.Id,
            CreatedAt = game.CreatedAt,
            LastPlayedAt = lastPlayedAt,
            Ruleset = game.Ruleset?.ToDto() ?? new RulesetDto
            {
                Id = game.RulesetId,
                Name = game.RulesetId
            },
            Scenario = game.Scenario?.ToDto() ?? new ScenarioDto
            {
                Id = game.ScenarioId,
                RulesetId = game.RulesetId,
                Name = game.ScenarioId
            },
            Player = game.Player?.ToDto() ?? new PlayerDto
            {
                Id = game.PlayerId,
                RulesetId = game.RulesetId,
                Name = game.PlayerId
            },
            Messages = messages
        };
    }

    public static partial GameDto[] ToDto(this IEnumerable<Game> games);
}