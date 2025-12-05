using System.Diagnostics;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.DocumentCracker.Services;

public class DocumentCrackingOrchestrator(
    ILogger<DocumentCrackingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    DocumentScanOptions options,
    IDocumentCrackingService documentCrackingService,
    ActivitySource activitySource)
{
    public async Task<DocumentCrackingSummary> CrackAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory configuration is required for document cracking.");
        }

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

            using Activity? directoryActivity = activitySource.StartActivity("DocumentCracking.ProcessDirectory");
            directoryActivity?.SetTag("cracker.directory", directory);
            directoryActivity?.SetTag("cracker.relative_directory", relativeDirectory);

            IEnumerable<string> files = directoryScanner.GetFiles(directory, options.SupportedExtensions);
            int directoryCracked = 0;
            int directoryFailures = 0;
            
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
                    string rulesetId = DocumentMetadataExtractor.ExtractRulesetId(relativeDirectory);
                    string documentKind = DocumentMetadataExtractor.DetermineDocumentKind(relativeDirectory);
                    await documentCrackingService.ProcessDocumentAsync(filePath, relativeDirectory, rulesetId, documentKind, cancellationToken);
                    summary.TotalCracked++;
                    directoryCracked++;
                }
                catch (Exception ex)
                {
                    summary.TotalFailures++;
                    directoryFailures++;
                    logger.LogError(ex, "Failed to crack document: {FilePath}", filePath);
                }
            }
            
            if (directoryActivity != null)
            {
                directoryActivity.SetTag("cracker.directory_cracked", directoryCracked);
                directoryActivity.SetTag("cracker.directory_failures", directoryFailures);
                directoryActivity.SetStatus(directoryFailures > 0 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            }
        }

        return summary;
    }


    public class DocumentCrackingSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalCracked { get; set; }
        public int TotalFailures { get; set; }
        public int SkippedUnsupported { get; set; }
    }
}


