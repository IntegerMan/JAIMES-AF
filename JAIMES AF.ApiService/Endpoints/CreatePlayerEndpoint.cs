using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class CreatePlayerEndpoint : Endpoint<CreatePlayerRequest, PlayerResponse>
{
    public required IPlayersService PlayersService { get; set; }

    public override void Configure()
    {
        Post("/players");
        AllowAnonymous();
        Description(b => b
            .Produces<PlayerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Players"));
    }

    public override async Task HandleAsync(CreatePlayerRequest req, CancellationToken ct)
    {
        try
        {
            PlayerDto player = await PlayersService.CreatePlayerAsync(
                req.Id,
                req.RulesetId,
                req.Description,
                req.Name,
                ct);

            PlayerResponse response = new()
            {
                Id = player.Id,
                RulesetId = player.RulesetId,
                Description = player.Description,
                Name = player.Name
            };

            await Send.CreatedAtAsync<GetPlayerEndpoint>(response, response, verb: Http.GET, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

