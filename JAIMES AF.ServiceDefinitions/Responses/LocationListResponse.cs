namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for a list of locations.
/// </summary>
public class LocationListResponse
{
    public LocationResponse[] Locations { get; set; } = [];
    public int TotalCount { get; set; }
}
