namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class ListLocationsEndpoint : EndpointWithoutRequest<LocationListResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/games/{gameId:guid}/locations");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationListResponse>(StatusCodes.Status200OK)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Guid gameId = Route<Guid>("gameId");
        LocationListResponse response = await LocationService.GetLocationsAsync(gameId, ct);
        await Send.OkAsync(response, ct);
    }
}
