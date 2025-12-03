using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a chunk of text extracted from a cracked document.
/// Chunks are stored with their text content and metadata for embedding generation.
/// </summary>
[Table("DocumentChunks")]
public class DocumentChunk
{
    /// <summary>
    /// Gets or sets the unique identifier for this chunk (auto-incrementing).
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the unique chunk ID (GUID-based string identifier).
    /// This is used for cross-referencing with Qdrant embeddings.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the cracked document this chunk belongs to.
    /// </summary>
    [Required]
    public int DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    [Required]
    [Column(TypeName = "text")]
    public string ChunkText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index of this chunk within the document (0-based).
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this chunk was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the Qdrant point ID for this chunk's embedding (if embedded).
    /// </summary>
    [MaxLength(100)]
    public string? QdrantPointId { get; set; }

    /// <summary>
    /// Navigation property to the cracked document this chunk belongs to.
    /// </summary>
    [ForeignKey(nameof(DocumentId))]
    public CrackedDocument? CrackedDocument { get; set; }
}
