namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record RecrackDocumentRequest
{
    public required string FilePath { get; init; }
    public string? RelativeDirectory { get; init; }
    public string? DocumentKind { get; init; }
    public string? RulesetId { get; init; }
}