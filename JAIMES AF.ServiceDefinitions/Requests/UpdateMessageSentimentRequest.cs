using System.ComponentModel.DataAnnotations;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// A request to manually update the sentiment of a message.
/// </summary>
public class UpdateMessageSentimentRequest
{
    /// <summary>
    /// Gets or sets the new sentiment value (-1, 0, 1).
    /// </summary>
    [Required]
    [Range(-1, 1)]
    public int Sentiment { get; set; }

    /// <summary>
    /// Gets or sets the source of the update: 1 (Player) or 2 (Admin).
    /// Defaults to Player if not specified.
    /// </summary>
    public int? Source { get; set; }
}

