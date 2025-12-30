using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents the sentiment analysis result for a conversation message.
/// Stored separately from Message to avoid table locking when writing sentiment after the fact.
/// </summary>
[Table("MessageSentiments")]
public class MessageSentiment
{
    /// <summary>
    /// Gets or sets the unique identifier for this sentiment record (auto-incrementing).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message this sentiment belongs to.
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the sentiment value: -1 (negative), 0 (neutral), 1 (positive).
    /// </summary>
    [Required]
    public int Sentiment { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for the sentiment prediction (0.0 to 1.0).
    /// Null for legacy records created before confidence tracking was implemented.
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this sentiment was first analyzed (UTC).
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when this sentiment was last updated (UTC).
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the message this sentiment belongs to.
    /// </summary>
    [ForeignKey(nameof(MessageId))]
    public Message? Message { get; set; }
}
