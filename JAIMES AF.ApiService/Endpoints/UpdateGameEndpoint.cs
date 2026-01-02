using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class UpdateGameEndpoint : Endpoint<UpdateGameRequest, GameInfoResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Put("/games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces<GameInfoResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(UpdateGameRequest req, CancellationToken ct)
    {
        string? gameIdStr = Route<string>("gameId", true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            ThrowError("Invalid game ID format");
            return;
        }

        GameDto? updatedGame =
            await GameService.UpdateGameAsync(gameId, req.Title, req.AgentId, req.InstructionVersionId, ct);
        if (updatedGame == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new GameInfoResponse
        {
            GameId = updatedGame.GameId,
            Title = updatedGame.Title,
            ScenarioId = updatedGame.Scenario.Id,
            ScenarioName = updatedGame.Scenario.Name,
            RulesetId = updatedGame.Ruleset.Id,
            RulesetName = updatedGame.Ruleset.Name,
            PlayerId = updatedGame.Player.Id,
            PlayerName = updatedGame.Player.Name,
            CreatedAt = updatedGame.CreatedAt,
            LastPlayedAt = updatedGame.LastPlayedAt
        }, ct);
    }
}
