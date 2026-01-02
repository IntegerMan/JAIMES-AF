namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class GetLocationEventsEndpoint : EndpointWithoutRequest<LocationEventResponse[]>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/locations/{locationId:int}/events");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationEventResponse[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int locationId = Route<int>("locationId");
        LocationEventResponse[]? events = await LocationService.GetLocationEventsAsync(locationId, ct);

        if (events == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(events, ct);
    }
}
