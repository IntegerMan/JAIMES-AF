using System.Text;
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

    public async Task<string> IndexDocumentAsync(string filePath, string indexName, CancellationToken cancellationToken = default)
    {
        string documentId = GetDocumentId(filePath, indexName);
        FileInfo fileInfo = new FileInfo(filePath);
        
        logger.LogInformation("Indexing document: {FilePath} with index: {IndexName}, documentId: {DocumentId}", filePath, indexName, documentId);
        
        return await memory.ImportDocumentAsync(
            new Document(documentId)
                .AddFile(filePath)
                .AddTag("type", "sourcebook")
                .AddTag("ruleset", indexName)
                .AddTag("fileName", fileInfo.Name),
            index: indexName,
            cancellationToken: cancellationToken);
    }

    private static string GetDocumentId(string filePath, string indexName)
    {
        // Get just the filename (no path)
        string fileName = Path.GetFileName(filePath);
        
        // Sanitize both ruleset ID and filename to only allow: letters, numbers, periods, underscores
        // Convert to lowercase and replace invalid characters
        string sanitizedRulesetId = SanitizeIdentifier(indexName);
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
        StringBuilder sb = new();
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

