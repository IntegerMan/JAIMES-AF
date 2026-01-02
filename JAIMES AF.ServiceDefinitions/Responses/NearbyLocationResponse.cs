namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for a nearby location relationship.
/// </summary>
public class NearbyLocationResponse
{
    public int SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = string.Empty;
    public int TargetLocationId { get; set; }
    public string TargetLocationName { get; set; } = string.Empty;
    public string? Distance { get; set; }
    public string? TravelNotes { get; set; }
    public string? StorytellerNotes { get; set; }
}
