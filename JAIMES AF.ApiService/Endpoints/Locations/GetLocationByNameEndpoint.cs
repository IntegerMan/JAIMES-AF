namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class GetLocationByNameRequest
{
    public string Name { get; set; } = string.Empty;
}

public class GetLocationByNameEndpoint : Endpoint<GetLocationByNameRequest, LocationResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/games/{gameId:guid}/locations/by-name/{name}");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(GetLocationByNameRequest req, CancellationToken ct)
    {
        Guid gameId = Route<Guid>("gameId");
        string? name = Route<string>("name");

        if (string.IsNullOrWhiteSpace(name))
        {
            ThrowError("Location name is required");
            return;
        }

        LocationResponse? response = await LocationService.GetLocationByNameAsync(gameId, name, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
