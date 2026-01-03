using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a significant event that has occurred at a location.
/// Events help the AI maintain historical consistency in storytelling.
/// </summary>
[Table("LocationEvents")]
public class LocationEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this event.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the location where this event occurred.
    /// </summary>
    [Required]
    public int LocationId { get; set; }

    /// <summary>
    /// Gets or sets the name/title of the event.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what happened during this event.
    /// </summary>
    [Required]
    public string EventDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this event record was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the location where this event occurred.
    /// </summary>
    public Location Location { get; set; } = null!;
}
