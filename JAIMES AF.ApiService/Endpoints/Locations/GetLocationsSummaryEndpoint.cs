using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Locations;

/// <summary>
/// Endpoint to get location summary statistics.
/// </summary>
public class GetLocationsSummaryEndpoint : EndpointWithoutRequest<LocationsSummaryResponse>
{
    public required ILocationService LocationService { get; set; }

    public override void Configure()
    {
        Get("/admin/locations/summary");
        AllowAnonymous();
        Description(b => b
            .Produces<LocationsSummaryResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Guid? gameId = Query<Guid?>("gameId", false);
        LocationsSummaryResponse response = await LocationService.GetLocationsSummaryAsync(gameId, ct);
        await Send.OkAsync(response, ct);
    }
}
