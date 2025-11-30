using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceLayer.Mapping;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GameStateEndpoint : EndpointWithoutRequest<GameStateResponse>
{
    public required IGameService GameService { get; set; }

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
        string? gameIdStr = Route<string>("gameId", isRequired: true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            ThrowError("Invalid game ID format");
        }
        GameDto? gameDto = await GameService.GetGameAsync(gameId, ct);

        if (gameDto == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        GameStateResponse gameState = new()
        {
            GameId = gameDto.GameId,
            Messages = (gameDto.Messages ?? []).Select(m => m.ToResponse()).ToArray(),
            RulesetId = gameDto.Ruleset.Id,
            RulesetName = gameDto.Ruleset.Name,
            ScenarioId = gameDto.Scenario.Id,
            ScenarioName = gameDto.Scenario.Name,
            PlayerId = gameDto.Player.Id,
            PlayerName = gameDto.Player.Name,
        };
        await Send.OkAsync(gameState, cancellation: ct);
    }
}
