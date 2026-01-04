namespace MattEland.Jaimes.ServiceDefinitions.Configuration;

/// <summary>
/// Configuration options for the document cracking service.
/// </summary>
public class DocumentCrackingOptions
{
    /// <summary>
    /// When true, documents will have their binary content uploaded when cracked
    /// for viewing in the admin UI. Defaults to true.
    /// </summary>
    public bool UploadDocumentsWhenCracking { get; set; } = true;
}
