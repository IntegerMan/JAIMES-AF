namespace MattEland.Jaimes.DocumentProcessing.Services;

public class DirectoryScanner(ILogger<DirectoryScanner> logger) : IDirectoryScanner
{
    public IEnumerable<string> GetSubdirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            logger.LogError("Source directory does not exist: {RootPath}", rootPath);
            throw new DirectoryNotFoundException($"Source directory does not exist: {rootPath}");
        }

        return Directory.EnumerateDirectories(rootPath);
    }

    public IEnumerable<string> GetFiles(string directoryPath, IEnumerable<string> supportedExtensions)
    {
        if (!Directory.Exists(directoryPath))
        {
            logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return [];
        }

        HashSet<string> extensions = new(supportedExtensions ?? [], StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(directoryPath)
            .Where(file => extensions.Contains(Path.GetExtension(file)));
    }
}