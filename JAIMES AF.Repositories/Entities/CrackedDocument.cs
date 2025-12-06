namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a document that has been "cracked" (text extracted) and is ready for chunking.
/// The extracted text content is stored in a JSONB column for efficient storage and querying.
/// </summary>
[Table("CrackedDocuments")]
public class CrackedDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for this cracked document.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the full file path to the source document.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative directory path from the content root.
    /// </summary>
    [MaxLength(2048)]
    public string RelativeDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name (without path).
    /// </summary>
    [Required]
    [MaxLength(512)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extracted text content from the document.
    /// This is stored as TEXT in PostgreSQL for efficient full-text search.
    /// </summary>
    [Required]
    [Column(TypeName = "text")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the document was cracked.
    /// </summary>
    [Required]
    public DateTime CrackedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the number of pages in the document (for PDFs).
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this document has been fully processed (chunked and embedded).
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// Gets or sets the total number of chunks created from this document.
    /// </summary>
    public int TotalChunks { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of chunks that have been processed (embedded).
    /// </summary>
    public int ProcessedChunkCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the kind of document (e.g., "Sourcebook", "Adventure", etc.).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DocumentKind { get; set; } = DocumentKinds.Sourcebook;

    /// <summary>
    /// Gets or sets the ruleset ID this document belongs to (e.g., "dnd5e").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string RulesetId { get; set; } = string.Empty;
}