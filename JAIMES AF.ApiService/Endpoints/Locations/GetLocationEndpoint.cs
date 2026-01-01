namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class GetLocationEndpoint : EndpointWithoutRequest<LocationResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/locations/{locationId:int}");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int locationId = Route<int>("locationId");
        LocationResponse? response = await LocationService.GetLocationByIdAsync(locationId, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
