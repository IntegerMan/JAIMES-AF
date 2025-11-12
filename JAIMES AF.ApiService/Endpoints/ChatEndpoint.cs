using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ChatEndpoint : Ep.Req<ChatRequest>.Res<GameStateResponse>
{
    public required IGameService GameService { get; set; }
    public required IChatService ChatService { get; set; }
    public required IChatHistoryService ChatHistoryService { get; set; }

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

        (string[] responses, string threadJson) = await ChatService.GetChatResponseAsync(gameDto, req.Message, ct);

        MessageResponse[] responseMessages = responses.Select(m => new MessageResponse
        {
            Text = m,
            Participant = ChatParticipant.GameMaster,
            PlayerId = null,
            ParticipantName = "Game Master",
            CreatedAt = DateTime.UtcNow
        })
            .ToArray();

        List<Message> messagesToPersist = [
            new() {
                GameId = gameDto.GameId,
                Text = req.Message,
                PlayerId = gameDto.PlayerId,
                CreatedAt = DateTime.UtcNow
            }
        ];
        messagesToPersist.AddRange(responseMessages.Select(m => new Message
        {
            GameId = gameDto.GameId,
            Text = m.Text,
            PlayerId = null,
            CreatedAt = m.CreatedAt
        }));

        await GameService.AddMessagesAsync(messagesToPersist, ct);

        // Get the last AI message ID (last message where PlayerId == null)
        // After SaveChangesAsync, EF Core will have populated the Id property
        int? lastAiMessageId = messagesToPersist
            .Where(m => m.PlayerId == null)
            .LastOrDefault()?.Id;

        // Save the thread JSON
        await ChatHistoryService.SaveThreadJsonAsync(gameDto.GameId, threadJson, lastAiMessageId, ct);

        GameStateResponse gameState = new()
        {
            GameId = gameDto.GameId,
            Messages = responseMessages,
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
