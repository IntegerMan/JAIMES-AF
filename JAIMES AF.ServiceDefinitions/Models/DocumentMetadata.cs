namespace MattEland.Jaimes.ServiceDefinitions.Models;

/// <summary>
/// Legacy model - replaced by EF Core entity in JAIMES AF.Repositories.Entities.DocumentMetadata
/// This class is kept for backward compatibility with messages.
/// </summary>
[Obsolete("Use MattEland.Jaimes.Repositories.Entities.DocumentMetadata instead")]
public class DocumentMetadata
{
    public string? Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime LastScanned { get; set; }
    public string DocumentKind { get; set; } = DocumentKinds.Sourcebook;
    public string RulesetId { get; set; } = string.Empty;
}