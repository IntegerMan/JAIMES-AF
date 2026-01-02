using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Exceptions;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

public class CreateLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? StorytellerNotes { get; set; }
}

public class CreateLocationEndpoint : Endpoint<CreateLocationRequest, LocationResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Post("/games/{gameId:guid}/locations");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("Locations"));
    }

    public override async Task HandleAsync(CreateLocationRequest req, CancellationToken ct)
    {
        Guid gameId = Route<Guid>("gameId");

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            ThrowError("Name is required");
            return;
        }

        if (req.Name.Length > 200)
        {
            ThrowError("Name must be 200 characters or less");
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Description))
        {
            ThrowError("Description is required");
            return;
        }

        try
        {
            LocationResponse result = await LocationService.CreateLocationAsync(
                gameId, req.Name, req.Description, req.StorytellerNotes, ct);

            await Send.CreatedAtAsync<GetLocationEndpoint>(
                new { locationId = result.Id },
                result,
                cancellation: ct);
        }
        catch (DuplicateResourceException ex)
        {
            ThrowError(ex.Message, StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (KeyNotFoundException ex)
        {
            AddError(ex.Message);
            await Send.NotFoundAsync(ct);
        }
    }
}
