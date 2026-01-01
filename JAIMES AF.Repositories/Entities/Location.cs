using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a location in a game world. Locations are scoped to individual games
/// and are used by the AI storyteller to maintain consistent world-building.
/// </summary>
[Table("Locations")]
public class Location
{
    /// <summary>
    /// Gets or sets the unique identifier for this location.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the game this location belongs to.
    /// Locations are not shared across games.
    /// </summary>
    [Required]
    public Guid GameId { get; set; }

    /// <summary>
    /// Gets or sets the name of the location. Must be unique within a game.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the player-facing description and appearance of the location.
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets private notes for the AI storyteller.
    /// These are not shown to players and help the AI plan story elements.
    /// </summary>
    public string? StorytellerNotes { get; set; }

    /// <summary>
    /// Gets or sets when this location was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this location was last updated.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the game this location belongs to.
    /// </summary>
    public Game Game { get; set; } = null!;

    /// <summary>
    /// Navigation property to events that have occurred at this location.
    /// </summary>
    public ICollection<LocationEvent> Events { get; set; } = new List<LocationEvent>();

    /// <summary>
    /// Navigation property to nearby location relationships (as source).
    /// </summary>
    public ICollection<NearbyLocation> NearbyLocationsAsSource { get; set; } = new List<NearbyLocation>();

    /// <summary>
    /// Navigation property to nearby location relationships (as target).
    /// </summary>
    public ICollection<NearbyLocation> NearbyLocationsAsTarget { get; set; } = new List<NearbyLocation>();
}
