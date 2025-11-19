using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace MattEland.Jaimes.Indexer.Services;

public class DocumentIndexer(ILogger<DocumentIndexer> logger, IKernelMemory memory) : IDocumentIndexer
{
    public async Task<bool> DocumentExistsAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            string documentId = GetDocumentId(filePath, indexName);
            DataPipelineStatus? status = await memory.GetDocumentStatusAsync(documentId, indexName, cancellationToken);
            return status is { Completed: true };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking if document exists: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> IndexDocumentAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            // Only log to logger, not to console - progress bars handle user feedback
            logger.LogWarning("File does not exist: {FilePath}", filePath);
            return false;
        }

        try
        {
            string documentId = GetDocumentId(filePath, indexName);
            string fileName = Path.GetFileName(filePath);
            
            await memory.ImportDocumentAsync(
                new Document(documentId)
                    .AddFile(filePath)
                    .AddTag("sourcePath", filePath)
                    .AddTag("fileName", fileName),
                index: indexName,
                cancellationToken: cancellationToken);

            // Success is shown via progress bars, no need for console logging
            return true;
        }
        catch (Exception ex)
        {
            // Only log to logger for debugging - progress bars show error status
            logger.LogError(ex, "Error indexing document: {FilePath}", filePath);
            return false;
        }
    }

    private static string GetDocumentId(string filePath, string indexName)
    {
        // Extract ruleset ID from indexName (remove "index-" prefix if present)
        string rulesetId = indexName.StartsWith("index-", StringComparison.OrdinalIgnoreCase)
            ? indexName[6..] // Remove "index-" prefix
            : indexName;
        
        // Get just the filename (no path)
        string fileName = Path.GetFileName(filePath);
        
        // Sanitize both ruleset ID and filename to only allow: letters, numbers, periods, underscores
        // Convert to lowercase and replace invalid characters
        string sanitizedRulesetId = SanitizeIdentifier(rulesetId);
        string sanitizedFileName = SanitizeIdentifier(fileName);
        
        // Combine as rulesetId-filename.extension
        return $"{sanitizedRulesetId}-{sanitizedFileName}";
    }
    
    private static string SanitizeIdentifier(string input)
    {
        // Convert to lowercase
        string normalized = input.ToLowerInvariant();
        
        // Only keep letters, numbers, periods, and underscores
        // Replace all other characters with nothing (remove them)
        System.Text.StringBuilder sb = new();
        foreach (char c in normalized)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '_')
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString();
    }
}

