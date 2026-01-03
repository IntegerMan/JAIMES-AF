namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for a location.
/// </summary>
public class LocationResponse
{
    public int Id { get; set; }
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? StorytellerNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int EventCount { get; set; }
    public int NearbyLocationCount { get; set; }
}
