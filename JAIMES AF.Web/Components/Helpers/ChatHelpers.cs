using Markdig;

namespace MattEland.Jaimes.Web.Components.Helpers;

public static class ChatHelpers
{
    // Converts markdown text to HTML for rendering in Game Master messages
    public static string RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown);
    }

    // Splits text into paragraphs for separate chat bubbles
    public static IEnumerable<string> SplitIntoParagraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Split by double newlines (standard paragraph breaks)
        string[] paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        // If no double newlines found, try splitting by single newlines
        if (paragraphs.Length == 1 && text.Contains('\n'))
        {
            paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        // If still only one paragraph, return it as-is
        if (paragraphs.Length == 1)
        {
            return [text.Trim()];
        }

        // Return trimmed paragraphs, filtering out empty ones
        return paragraphs
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p));
    }
}

