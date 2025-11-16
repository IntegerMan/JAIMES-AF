namespace MattEland.Jaimes.Indexer.Services;

public interface IDirectoryScanner
{
    IEnumerable<string> GetSubdirectories(string rootPath);
    IEnumerable<string> GetFiles(string directoryPath, IEnumerable<string> supportedExtensions);
}

