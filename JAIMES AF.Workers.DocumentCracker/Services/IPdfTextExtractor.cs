namespace MattEland.Jaimes.Workers.DocumentCracker.Services;

public interface IPdfTextExtractor
{
    (string Content, int PageCount) ExtractText(string filePath);
}
