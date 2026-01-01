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
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int locationId = Route<int>("locationId");
        LocationEventResponse[] events = await LocationService.GetLocationEventsAsync(locationId, ct);
        await Send.OkAsync(events, ct);
    }
}
