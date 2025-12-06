namespace MattEland.Jaimes.ApiService.Endpoints;

public class UpdatePlayerEndpoint : Endpoint<UpdatePlayerRequest, PlayerResponse>
{
    public required IPlayersService PlayersService { get; set; }

    public override void Configure()
    {
        Put("/players/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<PlayerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Players"));
    }

    public override async Task HandleAsync(UpdatePlayerRequest req, CancellationToken ct)
    {
        string? id = Route<string>("id", true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Player ID is required");
            return;
        }

        try
        {
            PlayerDto player = await PlayersService.UpdatePlayerAsync(
                id,
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

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}