using System.Text.RegularExpressions;
using Markdig;

namespace MattEland.Jaimes.Web.Components.Helpers;

public static class ChatHelpers
{
    // Computes up to two-letter initials for an avatar based on a participant name.
    // Examples: "Game Master" -> "GM", "Player Character" -> "PC", "Emie von Laurentz" -> "EL", "Madonna" -> "MA"
    public static string GetAvatarInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";

        // Normalize whitespace and split into parts
        var parts = Regex.Split(name.Trim(), "\\s+")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // Common particles to ignore when choosing initials (e.g., 'von' in names)
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "von", "van", "de", "la", "le", "da", "di", "del", "des", "der", "den", "the", "and", "of"
        };

        var meaningful = parts.Where(p => !ignore.Contains(p)).ToList();
        if (!meaningful.Any()) meaningful = parts;

        // If there's only one meaningful part, use up to first two letters
        if (meaningful.Count == 1)
        {
            var token = Regex.Replace(meaningful[0], "[^\\p{L}]", ""); // keep letters only
            if (string.IsNullOrEmpty(token)) return "?";
            token = token.ToUpperInvariant();
            return token.Length == 1 ? token : token.Substring(0, Math.Min(2, token.Length));
        }

        // Use first letter of first and last meaningful parts
        char first = meaningful.First()[0];
        char last = meaningful.Last()[0];
        return string.Concat(char.ToUpperInvariant(first), char.ToUpperInvariant(last));
    }

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

