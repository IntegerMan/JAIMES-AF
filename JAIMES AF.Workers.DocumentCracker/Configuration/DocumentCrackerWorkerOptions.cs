namespace MattEland.Jaimes.Workers.DocumentCracker.Configuration;

public class DocumentCrackerWorkerOptions
{
    public string? SourceDirectory { get; set; }
    public List<string> SupportedExtensions { get; set; } = [".pdf"];
    
    /// <summary>
    /// When true, documents will have their binary content uploaded to the database
    /// for viewing in the admin UI. Defaults to true.
    /// </summary>
    public bool UploadDocumentsToDatabase { get; set; } = true;
}







