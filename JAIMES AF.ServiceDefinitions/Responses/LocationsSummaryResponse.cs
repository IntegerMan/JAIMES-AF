namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing location summary statistics.
/// </summary>
public record LocationsSummaryResponse
{
    /// <summary>
    /// Total count of all locations.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Count of locations that have at least one event.
    /// </summary>
    public int WithEventsCount { get; init; }

    /// <summary>
    /// Count of locations that have no nearby location links (orphaned).
    /// </summary>
    public int OrphanedCount { get; init; }

    /// <summary>
    /// Breakdown of location counts by game.
    /// Key is the game ID, value contains game title and location count.
    /// </summary>
    public Dictionary<Guid, GameLocationCount> ByGame { get; init; } = new();
}

/// <summary>
/// Location count for a specific game.
/// </summary>
public record GameLocationCount
{
    /// <summary>
    /// The game's display title.
    /// </summary>
    public string GameTitle { get; init; } = string.Empty;

    /// <summary>
    /// Number of locations in this game.
    /// </summary>
    public int LocationCount { get; init; }
}
