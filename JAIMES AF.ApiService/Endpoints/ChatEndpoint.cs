using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ChatEndpoint : Ep.Req<ChatRequest>.Res<GameStateResponse>
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Post("games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces<GameStateResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
    {
        string? gameIdStr = Route<string>("gameId", isRequired: true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            ThrowError("Invalid game ID format");
        }
        // ProcessChatMessageAsync will throw if game doesn't exist, so we can get the game after
        ChatResponse chatResponse = await GameService.ProcessChatMessageAsync(gameId, req.Message, ct);

        // Get the game to populate the response (game exists since ProcessChatMessageAsync succeeded)
        GameDto? gameDto = await GameService.GetGameAsync(gameId, ct);
        if (gameDto == null)
        {
            // This shouldn't happen, but handle it gracefully
            await Send.NotFoundAsync(ct);
            return;
        }

        GameStateResponse gameState = new()
        {
            GameId = gameDto.GameId,
            Messages = chatResponse.Messages,
            RulesetId = gameDto.RulesetId,
            RulesetName = gameDto.RulesetName,
            ScenarioId = gameDto.ScenarioId,
            ScenarioName = gameDto.ScenarioName,
            PlayerId = gameDto.PlayerId,
            PlayerName = gameDto.PlayerName,
        };

        await Send.OkAsync(gameState, cancellation: ct);

    }
}
