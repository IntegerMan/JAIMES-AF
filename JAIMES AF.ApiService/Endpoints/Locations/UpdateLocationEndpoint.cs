using FastEndpoints;

namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class UpdateLocationRequest
{
    public string? Description { get; set; }
    public string? StorytellerNotes { get; set; }
}

public class UpdateLocationEndpoint : Endpoint<UpdateLocationRequest, LocationResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Put("/locations/{locationId:int}");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(UpdateLocationRequest req, CancellationToken ct)
    {
        int locationId = Route<int>("locationId");

        if (string.IsNullOrWhiteSpace(req.Description))
        {
            ThrowError("Description is required");
            return;
        }

        LocationResponse? result = await LocationService.UpdateLocationAsync(
            locationId, req.Description, req.StorytellerNotes, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
