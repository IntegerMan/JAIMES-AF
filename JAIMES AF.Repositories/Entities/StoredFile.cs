using System.ComponentModel.DataAnnotations;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Entity for storing files and binary content in PostgreSQL.
/// Designed to be reusable for various file types (reports, images, etc.).
/// </summary>
public class StoredFile
{
    public int Id { get; set; }

    /// <summary>
    /// The kind of item this file represents (e.g., "TestReport", "Image", "Document").
    /// Used for categorization and querying.
    /// </summary>
    [MaxLength(50)]
    public required string ItemKind { get; set; }

    /// <summary>
    /// Original or descriptive filename.
    /// </summary>
    [MaxLength(255)]
    public required string FileName { get; set; }

    /// <summary>
    /// MIME content type (e.g., "text/html", "image/png", "application/pdf").
    /// </summary>
    [MaxLength(100)]
    public required string ContentType { get; set; }

    /// <summary>
    /// Text content for HTML, JSON, or other text-based files.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Binary content for images, PDFs, or other binary files.
    /// Stored as PostgreSQL bytea type.
    /// </summary>
    public byte[]? BinaryContent { get; set; }

    /// <summary>
    /// When the file was created/stored.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// File size in bytes (for display/management purposes).
    /// </summary>
    public long? SizeBytes { get; set; }
}
