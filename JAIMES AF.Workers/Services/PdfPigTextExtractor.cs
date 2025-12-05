using System.Text;
using MattEland.Jaimes.ServiceDefinitions.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.Workers.Services;

/// <summary>
/// PDF text extractor implementation using PdfPig.
/// </summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public (string content, int pageCount) ExtractText(string filePath)
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
}
