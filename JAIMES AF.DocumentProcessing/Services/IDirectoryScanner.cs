namespace MattEland.Jaimes.DocumentProcessing.Services;

public interface IDirectoryScanner
{
    IEnumerable<string> GetSubdirectories(string rootPath);
    IEnumerable<string> GetFiles(string directoryPath, IEnumerable<string> supportedExtensions);
}


