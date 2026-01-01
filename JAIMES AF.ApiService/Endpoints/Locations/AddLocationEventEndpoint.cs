using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class AddLocationEventRequest
{
    public string EventName { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
}

public class AddLocationEventEndpoint : Endpoint<AddLocationEventRequest, LocationEventResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Post("/locations/{locationId:int}/events");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationEventResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(AddLocationEventRequest req, CancellationToken ct)
    {
        int locationId = Route<int>("locationId");

        if (string.IsNullOrWhiteSpace(req.EventName))
        {
            ThrowError("Event name is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(req.EventDescription))
        {
            ThrowError("Event description is required");
            return;
        }

        // Check if location exists
        LocationResponse? location = await LocationService.GetLocationByIdAsync(locationId, ct);
        if (location == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        LocationEventResponse result = await LocationService.AddLocationEventAsync(
            locationId, req.EventName, req.EventDescription, ct);

        await Send.CreatedAtAsync<GetLocationEventsEndpoint>(
            new { locationId },
            result,
            cancellation: ct);
    }
}
