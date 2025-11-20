using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Indexer.Services;

public class DirectoryScanner(ILogger<DirectoryScanner> logger) : IDirectoryScanner
{
    public IEnumerable<string> GetSubdirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            logger.LogError("Source directory does not exist: {RootPath}", rootPath);
            throw new DirectoryNotFoundException($"Source directory does not exist: {rootPath}");
        }

        // Suppress informational logging - progress bars show this information
        return Directory.EnumerateDirectories(rootPath);
    }

    public IEnumerable<string> GetFiles(string directoryPath, IEnumerable<string> supportedExtensions)
    {
        if (!Directory.Exists(directoryPath))
        {
            // Only log warnings/errors, not informational messages
            logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return [];
        }

        HashSet<string> extensions = new(supportedExtensions, StringComparer.OrdinalIgnoreCase);
        
        // Suppress debug/info logging - progress bars show file processing
        return Directory.EnumerateFiles(directoryPath)
            .Where(file => extensions.Contains(Path.GetExtension(file)));
    }
}

