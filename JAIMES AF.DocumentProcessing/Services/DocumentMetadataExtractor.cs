namespace MattEland.Jaimes.DocumentProcessing.Services;

/// <summary>
/// Utility class for extracting metadata (rulesetId, documentKind) from document paths.
/// Reused across DocumentScanner, DocumentChangeDetector, and DocumentCracking services.
/// </summary>
public static class DocumentMetadataExtractor
{
    /// <summary>
    /// Extracts the ruleset ID from the relative directory path.
    /// The ruleset ID is the first directory segment (e.g., "dnd5e" from "dnd5e" or "dnd5e/sourcebooks/...").
    /// </summary>
    public static string ExtractRulesetId(string? relativeDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectory)) return "default";

        string[] parts = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0 ? parts[0] : "default";
    }

    /// <summary>
    /// Determines the document kind from the relative directory path.
    /// For now, defaults to "Sourcebook" but can be extended to extract from path structure in the future.
    /// </summary>
    public static string DetermineDocumentKind(string? relativeDirectory)
    {
        // For now, all documents are Sourcebooks
        // In the future, this could extract from the path structure (e.g., "dnd5e/sourcebooks/..." or "dnd5e/transcripts/...")
        return DocumentKinds.Sourcebook;
    }
}