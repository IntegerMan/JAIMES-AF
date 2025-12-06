namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record DocumentStatusResponse
{
    public required DocumentStatusInfo[] Documents { get; init; } = [];
}

public record DocumentStatusInfo
{
    public string? DocumentId { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string RelativeDirectory { get; init; }
    public required bool IsCracked { get; init; }
    public required bool HasEmbeddings { get; init; }
    public DateTime? CrackedAt { get; init; }
    public long? FileSize { get; init; }
    public int? PageCount { get; init; }
    public string? DocumentKind { get; init; }
    public string? RulesetId { get; init; }
}