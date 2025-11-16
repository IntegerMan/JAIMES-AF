using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class GetPlayerEndpoint : EndpointWithoutRequest<PlayerResponse>
{
    public required IPlayersService PlayersService { get; set; }

    public override void Configure()
    {
        Get("/players/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<PlayerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Players"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? id = Route<string>("id", isRequired: true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Player ID is required");
            return;
        }

        try
        {
            PlayerDto player = await PlayersService.GetPlayerAsync(id, ct);

            PlayerResponse response = new()
            {
                Id = player.Id,
                RulesetId = player.RulesetId,
                Description = player.Description,
                Name = player.Name
            };

            await Send.OkAsync(response, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

