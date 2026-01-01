namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class GetNearbyLocationsEndpoint : EndpointWithoutRequest<NearbyLocationResponse[]>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/locations/{locationId:int}/nearby");
        AllowAnonymous();
        Description(b => b
            .Produces<NearbyLocationResponse[]>(StatusCodes.Status200OK)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int locationId = Route<int>("locationId");
        NearbyLocationResponse[] nearby = await LocationService.GetNearbyLocationsAsync(locationId, ct);
        await Send.OkAsync(nearby, ct);
    }
}
