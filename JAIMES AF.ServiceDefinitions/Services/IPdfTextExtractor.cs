namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Abstraction for extracting text and page count from a PDF file.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Extracts textual content from the given PDF file.
    /// Returns the full text content and the total page count.
    /// </summary>
    /// <param name="filePath">Absolute path to a PDF file.</param>
    /// <returns>A tuple of extracted text content and page count.</returns>
    (string content, int pageCount) ExtractText(string filePath);
}