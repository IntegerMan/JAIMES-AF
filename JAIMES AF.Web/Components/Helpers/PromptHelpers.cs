namespace MattEland.Jaimes.Web.Components.Helpers;

/// <summary>
/// Helper methods for processing AI-generated prompts.
/// </summary>
public static class PromptHelpers
{
    /// <summary>
    /// Common heading patterns that LLMs might add to generated prompts.
    /// </summary>
    private static readonly string[] HeadingsToStrip =
    [
        "## Improved Prompt",
        "# Improved Prompt",
        "**Improved Prompt**",
        "Improved Prompt:"
    ];

    /// <summary>
    /// Strips common leading headings from AI-generated prompts (e.g., "## Improved Prompt").
    /// </summary>
    /// <param name="prompt">The prompt text to clean.</param>
    /// <returns>The prompt with leading headings removed.</returns>
    public static string StripLeadingHeading(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt;

        var trimmed = prompt.TrimStart();
        foreach (var heading in HeadingsToStrip)
        {
            if (trimmed.StartsWith(heading, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[heading.Length..].TrimStart();
                break;
            }
        }

        return trimmed;
    }
}
