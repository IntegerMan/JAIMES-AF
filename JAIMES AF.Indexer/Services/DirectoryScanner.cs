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

        logger.LogDebug("Scanning for subdirectories in: {RootPath}", rootPath);
        IEnumerable<string> subdirectories = Directory.EnumerateDirectories(rootPath);
        
        logger.LogInformation("Found {Count} subdirectories in {RootPath}", subdirectories.Count(), rootPath);
        return subdirectories;
    }

    public IEnumerable<string> GetFiles(string directoryPath, IEnumerable<string> supportedExtensions)
    {
        if (!Directory.Exists(directoryPath))
        {
            logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return Enumerable.Empty<string>();
        }

        HashSet<string> extensions = new(supportedExtensions, StringComparer.OrdinalIgnoreCase);
        
        logger.LogDebug("Scanning for files in: {DirectoryPath}", directoryPath);
        IEnumerable<string> files = Directory.EnumerateFiles(directoryPath)
            .Where(file => extensions.Contains(Path.GetExtension(file)));
        
        logger.LogDebug("Found {Count} indexable files in {DirectoryPath}", files.Count(), directoryPath);
        return files;
    }
}

