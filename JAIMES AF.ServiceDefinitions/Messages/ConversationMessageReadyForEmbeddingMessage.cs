using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message queued for embedding generation for a conversation message.
/// Used to index conversation messages in both Qdrant and PostgreSQL for semantic search.
/// </summary>
public class ConversationMessageReadyForEmbeddingMessage
{
    /// <summary>
    /// Gets or sets the database ID of the message.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the game ID associated with the message.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Gets or sets the text content of the message to be embedded.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the message (User or Assistant).
    /// </summary>
    public ChatRole Role { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

