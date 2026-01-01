using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a proximity relationship between two locations.
/// This is a many-to-many join table with additional metadata.
/// </summary>
[Table("NearbyLocations")]
public class NearbyLocation
{
    /// <summary>
    /// Gets or sets the ID of the source location.
    /// </summary>
    [Required]
    public int SourceLocationId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the target/nearby location.
    /// </summary>
    [Required]
    public int TargetLocationId { get; set; }

    /// <summary>
    /// Gets or sets the distance between locations (e.g., "2 miles", "a day's journey").
    /// </summary>
    [MaxLength(100)]
    public string? Distance { get; set; }

    /// <summary>
    /// Gets or sets notes about traveling between these locations
    /// (e.g., "through the dark forest", "across the river").
    /// </summary>
    public string? TravelNotes { get; set; }

    /// <summary>
    /// Gets or sets private notes for the AI storyteller about this connection.
    /// Use this to hide secrets like dangers, shortcuts, or plot-relevant information
    /// that the player shouldn't know about yet.
    /// </summary>
    public string? StorytellerNotes { get; set; }

    /// <summary>
    /// Navigation property to the source location.
    /// </summary>
    public Location SourceLocation { get; set; } = null!;

    /// <summary>
    /// Navigation property to the target/nearby location.
    /// </summary>
    public Location TargetLocation { get; set; } = null!;
}
