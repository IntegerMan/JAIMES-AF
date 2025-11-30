namespace MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;

public interface IPdfTextExtractor
{
 (string content, int pageCount) ExtractText(string filePath);
}
