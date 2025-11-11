using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class DeleteGameEndpoint : Ep.NoReq.NoRes
{
    public required IGameService GameService { get; set; }

    public override void Configure()
    {
        Delete("/games/{gameId:guid}");
        AllowAnonymous();
        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Games"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? gameIdStr = Route<string>("gameId", isRequired: true);
        if (!Guid.TryParse(gameIdStr, out Guid gameId))
        {
            ThrowError("Invalid game ID format");
            return;
        }

        try
        {
            await GameService.DeleteGameAsync(gameId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (ArgumentException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

