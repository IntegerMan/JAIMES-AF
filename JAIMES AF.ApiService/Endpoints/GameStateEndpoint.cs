namespace MattEland.Jaimes.ApiService.Endpoints;

public class GameStateEndpoint : EndpointWithoutRequest<GameStateResponse>
{
    public required IGameService GameService { get; set; }
    public required IChatHistoryService ChatHistoryService { get; set; }
    public required IEvaluatorMetricCountService EvaluatorMetricCountService { get; set; }

    public override void Configure()
    {
        Get("/games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces<GameStateResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? gameIdStr = Route<string>("gameId", true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId)) ThrowError("Invalid game ID format");
        GameDto? gameDto = await GameService.GetGameAsync(gameId, ct);

        if (gameDto == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Get thread JSON from chat history
        string? threadJson = await ChatHistoryService.GetMostRecentThreadJsonAsync(gameId, ct);

        // Get expected metric count for progress UI
        int expectedMetricCount = EvaluatorMetricCountService.GetTotalExpectedMetricCount();

        // Map messages and set expected metric count for assistant messages
        var messages = (gameDto.Messages ?? []).Select(m =>
        {
            var response = m.ToResponse();
            // Only assistant messages (no PlayerId) get evaluated
            if (string.IsNullOrEmpty(m.PlayerId))
            {
                response = response with { ExpectedMetricCount = expectedMetricCount };
            }
            return response;
        }).ToArray();

        GameStateResponse gameState = new()
        {
            GameId = gameDto.GameId,
            Title = gameDto.Title,
            Messages = messages,
            RulesetId = gameDto.Ruleset.Id,
            RulesetName = gameDto.Ruleset.Name,
            ScenarioId = gameDto.Scenario.Id,
            ScenarioName = gameDto.Scenario.Name,
            PlayerId = gameDto.Player.Id,
            PlayerName = gameDto.Player.Name,
            CreatedAt = gameDto.CreatedAt,
            LastPlayedAt = gameDto.LastPlayedAt,
            AgentId = gameDto.AgentId,
            AgentName = gameDto.AgentName,
            InstructionVersionId = gameDto.InstructionVersionId,
            VersionNumber = gameDto.VersionNumber,
            ThreadJson = threadJson
        };
        await Send.OkAsync(gameState, ct);
    }
}