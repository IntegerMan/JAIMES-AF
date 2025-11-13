using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ChatEndpoint : Ep.Req<ChatRequest>.Res<GameStateResponse>
{
    public required IGameService GameService { get; set; }
    public required IChatService ChatService { get; set; }

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
        GameDto? gameDto = await GameService.GetGameAsync(gameId, ct);

        if (gameDto == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        ChatResponse chatResponse = await ChatService.ProcessChatMessageAsync(gameId, req.Message, ct);

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
