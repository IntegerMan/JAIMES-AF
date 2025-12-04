using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents metadata about a document that has been scanned for processing.
/// This tracks the file path, hash, and last scan time to detect changes.
/// </summary>
[Table("DocumentMetadata")]
public class DocumentMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for this document metadata record.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the full file path to the document.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA256 hash of the file content for change detection.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this document was last scanned.
    /// </summary>
    [Required]
    public DateTime LastScanned { get; set; }

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
