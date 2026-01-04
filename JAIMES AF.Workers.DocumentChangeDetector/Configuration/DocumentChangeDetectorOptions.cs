namespace MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;

public class DocumentChangeDetectorOptions
{
    public string? ContentDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".pdf"];

    /// <summary>
    /// When true, detected documents will have their binary content uploaded when cracked
    /// for viewing in the admin UI. Defaults to true.
    /// </summary>
    public bool UploadDocumentsWhenCracking { get; set; } = true;
}