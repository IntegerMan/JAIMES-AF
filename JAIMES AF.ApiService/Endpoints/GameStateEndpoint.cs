using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

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
            Messages = (gameDto.Messages ?? Array.Empty<MessageDto>()).Select(m => new MessageResponse
                {
                    Text = m.Text,
                    Participant = string.IsNullOrEmpty(m.PlayerId)
                        ? ChatParticipant.GameMaster
                        : ChatParticipant.Player,
                    PlayerId = m.PlayerId,
                    ParticipantName = m.ParticipantName,
                    CreatedAt = m.CreatedAt
                })
                .ToArray(),
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
