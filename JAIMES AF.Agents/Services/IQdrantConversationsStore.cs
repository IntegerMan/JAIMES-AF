namespace MattEland.Jaimes.Agents.Services;

public interface IQdrantConversationsStore
{
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);

    Task StoreConversationAsync(
        string messageId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default);

    Task<List<ConversationSearchHit>> SearchConversationsAsync(
        float[] queryEmbedding,
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from Qdrant search containing basic message information.
/// </summary>
public record ConversationSearchHit
{
    public required int MessageId { get; init; }
    public required string Text { get; init; }
    public required Guid GameId { get; init; }
    public required string Role { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required float Score { get; init; }
}

