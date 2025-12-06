namespace MattEland.Jaimes.Web.Components.Helpers;

public static class ChatHelpers
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex HeaderRemovalRegex = new(
        @"^\s*#+\s.*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex EmptyLineCleanupRegex = new(
        @"\n\s*\n\s*\n+",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex HeaderDetectionRegex = new(
        @"^\s*#+\s",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex BoldHeaderRegex = new(
        @"^\*\*[^*]+\*\*:?\s*$",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    // Converts markdown text to HTML for rendering in Game Master messages
    // Filters out markdown headers (lines starting with #) before rendering
    public static string RenderMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        // Remove markdown headers (lines starting with one or more # characters)
        // This regex matches lines that start with optional whitespace followed by one or more # characters
        string filteredMarkdown = HeaderRemovalRegex.Replace(markdown, string.Empty);

        // Clean up multiple consecutive empty lines that may result from header removal
        filteredMarkdown = EmptyLineCleanupRegex.Replace(filteredMarkdown, "\n\n");

        return Markdown.ToHtml(filteredMarkdown);
    }

    // Checks if a paragraph looks like a header (e.g., "**Opening Message:**" or "# Header")
    public static bool IsHeaderLike(string? paragraph)
    {
        if (string.IsNullOrWhiteSpace(paragraph)) return false;

        string trimmed = paragraph.Trim();

        // Check for markdown headers (lines starting with #)
        if (HeaderDetectionRegex.IsMatch(trimmed)) return true;

        // Check for bold text that looks like a header/label (e.g., "**Opening Message:**" or "**Title**")
        // Pattern: starts with **, ends with ** or **:, and is relatively short (likely a label, not content)
        if (BoldHeaderRegex.IsMatch(trimmed)) return true;

        return false;
    }

    // Splits text into paragraphs for separate chat bubbles
    // Filters out header-like paragraphs (markdown headers and bold labels)
    public static IEnumerable<string> SplitIntoParagraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        // Split by double newlines (standard paragraph breaks)
        string[] paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);

        // If no double newlines found, try splitting by single newlines
        if (paragraphs.Length == 1 && text.Contains('\n'))
            paragraphs = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

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