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

        return ToDto(game, lastPlayedAt, messages);
    }

    /// <summary>
    /// Maps a Game to GameDto with optional LastPlayedAt and Messages.
    /// Used for efficient list queries where messages are not loaded.
    /// </summary>
    public static GameDto ToDto(this Game game, DateTime? lastPlayedAt, MessageDto[]? messages = null)
    {
        // Generate default title if not set: "PlayerName in ScenarioName (RulesetId)"
        string? title = game.Title;
        if (string.IsNullOrEmpty(title))
        {
            string playerName = game.Player?.Name ?? game.PlayerId;
            string scenarioName = game.Scenario?.Name ?? game.ScenarioId;
            string rulesetAbbrev = game.RulesetId;
            title = $"{playerName} in {scenarioName} ({rulesetAbbrev})";
        }

        return new GameDto
        {
            GameId = game.Id,
            Title = title,
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
            Messages = messages,
            AgentId = game.AgentId ?? game.Scenario?.ScenarioAgents?.FirstOrDefault()?.AgentId,
            AgentName = game.Agent?.Name ?? game.Scenario?.ScenarioAgents?.FirstOrDefault()?.Agent?.Name,
            InstructionVersionId = game.InstructionVersionId ??
                                   game.Scenario?.ScenarioAgents?.FirstOrDefault()?.InstructionVersionId,
            VersionNumber = game.InstructionVersion?.VersionNumber ??
                            game.Scenario?.ScenarioAgents?.FirstOrDefault()?.InstructionVersion?.VersionNumber
        };
    }

    public static GameDto[] ToDto(this IEnumerable<Game> games)
    {
        return games.Select(g => g.ToDto()).ToArray();
    }
}