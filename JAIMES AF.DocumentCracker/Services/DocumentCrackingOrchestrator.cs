using System.Linq;
using System.Text;
using MattEland.Jaimes.DocumentCracker.Models;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.DocumentCracker.Services;

public class DocumentCrackingOrchestrator(
    ILogger<DocumentCrackingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    DocumentScanOptions options,
    IMongoClient mongoClient)
{
    public async Task<DocumentCrackingSummary> CrackAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory configuration is required for document cracking.");
        }

        // Get database from connection string - the database name is "documents" as configured in AppHost
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        DocumentCrackingSummary summary = new();

        List<string> directories = directoryScanner
            .GetSubdirectories(options.SourceDirectory)
            .ToList();
        directories.Insert(0, options.SourceDirectory);

        foreach (string directory in directories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            string relativeDirectory = Path.GetRelativePath(options.SourceDirectory, directory);
            if (relativeDirectory == ".")
            {
                relativeDirectory = string.Empty;
            }

            IEnumerable<string> files = directoryScanner.GetFiles(directory, options.SupportedExtensions);
            foreach (string filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                summary.TotalDiscovered++;

                if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
                    summary.SkippedUnsupported++;
                    continue;
                }

                try
                {
                    await CrackDocumentAsync(filePath, relativeDirectory, collection, cancellationToken);
                    summary.TotalCracked++;
                }
                catch (Exception ex)
                {
                    summary.TotalFailures++;
                    logger.LogError(ex, "Failed to crack document: {FilePath}", filePath);
                }
            }
        }

        return summary;
    }

    private async Task CrackDocumentAsync(string filePath, string relativeDirectory, IMongoCollection<CrackedDocument> collection, CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new(filePath);
        (string contents, int pageCount) = ExtractPdfText(filePath);

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
            .Set(d => d.FilePath, filePath)
            .Set(d => d.RelativeDirectory, relativeDirectory)
            .Set(d => d.FileName, Path.GetFileName(filePath))
            .Set(d => d.Content, contents)
            .Set(d => d.CrackedAt, DateTime.UtcNow)
            .Set(d => d.FileSize, fileInfo.Length)
            .Set(d => d.PageCount, pageCount);
        
        UpdateOptions updateOptions = new() { IsUpsert = true };
        
        await collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken);
        logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
    }

    private static (string content, int pageCount) ExtractPdfText(string filePath)
    {
        StringBuilder builder = new();
        using PdfDocument document = PdfDocument.Open(filePath);
        int pageCount = 0;
        
        foreach (Page page in document.GetPages())
        {
            pageCount++;
            builder.AppendLine($"--- Page {page.Number} ---");
            string pageText = ContentOrderTextExtractor.GetText(page);
            builder.AppendLine(pageText);
            builder.AppendLine();
        }

        return (builder.ToString(), pageCount);
    }

    public class DocumentCrackingSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalCracked { get; set; }
        public int TotalFailures { get; set; }
        public int SkippedUnsupported { get; set; }
    }
}


