using Markdig;
using System.Text.RegularExpressions;

namespace MattEland.Jaimes.Web.Components.Helpers;

public static class ChatHelpers
{
    // Converts markdown text to HTML for rendering in Game Master messages
    // Filters out markdown headers (lines starting with #) before rendering
    public static string RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        // Remove markdown headers (lines starting with one or more # characters)
        // This regex matches lines that start with optional whitespace followed by one or more # characters
        string filteredMarkdown = Regex.Replace(
            markdown,
            @"^\s*#+\s.*$",
            string.Empty,
            RegexOptions.Multiline);

        // Clean up multiple consecutive empty lines that may result from header removal
        filteredMarkdown = Regex.Replace(filteredMarkdown, @"\n\s*\n\s*\n+", "\n\n", RegexOptions.Multiline);

        return Markdown.ToHtml(filteredMarkdown);
    }

    // Checks if a paragraph looks like a header (e.g., "**Opening Message:**" or "# Header")
    public static bool IsHeaderLike(string? paragraph)
    {
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            return false;
        }

        string trimmed = paragraph.Trim();

        // Check for markdown headers (lines starting with #)
        if (Regex.IsMatch(trimmed, @"^\s*#+\s"))
        {
            return true;
        }

        // Check for bold text that looks like a header/label (e.g., "**Opening Message:**" or "**Title**")
        // Pattern: starts with **, ends with ** or **:, and is relatively short (likely a label, not content)
        if (Regex.IsMatch(trimmed, @"^\*\*[^*]+\*\*:?\s*$"))
        {
            return true;
        }

        return false;
    }

    // Splits text into paragraphs for separate chat bubbles
    // Filters out header-like paragraphs (markdown headers and bold labels)
    public static IEnumerable<string> SplitIntoParagraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Split by double newlines (standard paragraph breaks)
        string[] paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);

        // If no double newlines found, try splitting by single newlines
        if (paragraphs.Length == 1 && text.Contains('\n'))
        {
            paragraphs = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        }

        // If still only one paragraph, return it as-is (unless it's header-like)
        if (paragraphs.Length == 1)
        {
            string trimmed = text.Trim();
            return IsHeaderLike(trimmed) ? [] : [trimmed];
        }

        // Return trimmed paragraphs, filtering out empty ones and header-like paragraphs
        return paragraphs
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p) && !IsHeaderLike(p));
    }
}

