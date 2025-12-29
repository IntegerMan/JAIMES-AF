namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents the embedding vector for a conversation message.
/// Stores the embedding in both Qdrant (for similarity search) and PostgreSQL (for persistence).
/// </summary>
[Table("MessageEmbeddings")]
public class MessageEmbedding
{
    /// <summary>
    /// Gets or sets the unique identifier for this embedding (auto-incrementing).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message this embedding belongs to.
    /// </summary>
    [Required]
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets the Qdrant point ID for this message's embedding (if embedded).
    /// </summary>
    [MaxLength(100)]
    public string? QdrantPointId { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector for this message.
    /// Stored in PostgreSQL using the pgvector extension.
    /// 
    /// NOTE: The embedding values stored here will NOT match the values stored in Qdrant for the same message.
    /// This is expected behavior:
    /// - Qdrant automatically normalizes vectors when using Cosine distance metric (scales to unit length)
    /// - PostgreSQL (pgvector) stores vectors as-is without normalization
    /// Both stores use the same original embedding, but Qdrant applies normalization for efficient cosine similarity calculations.
    /// </summary>
    [Column(TypeName = "vector")]
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this embedding was created.
    /// </summary>
    [Required]
    public DateTime EmbeddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the message this embedding belongs to.
    /// </summary>
    [ForeignKey(nameof(MessageId))]
    public Message? Message { get; set; }
}

