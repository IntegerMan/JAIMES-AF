using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.ServiceDefinitions.Messages;

/// <summary>
/// Message queued for asynchronous processing of conversation messages.
/// Used for quality control analysis of AI messages and sentiment analysis of user messages.
/// </summary>
public class ConversationMessageQueuedMessage
{
    /// <summary>
    /// The database ID of the message (for retrieval)
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// The game ID associated with the message
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// The role of the message (User or Assistant)
    /// </summary>
    public ChatRole Role { get; set; }
}

