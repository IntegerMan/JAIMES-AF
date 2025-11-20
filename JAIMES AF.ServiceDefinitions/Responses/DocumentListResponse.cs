namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record IndexListResponse
{
    public required string[] Indexes { get; init; }
}

public record DocumentListResponse
{
    public required string IndexName { get; init; }
    public required IndexedDocumentInfo[] Documents { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record IndexedDocumentInfo
{
    public required string DocumentId { get; init; }
    public required string Index { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
    public DateTime? LastUpdate { get; init; }
    public string? Status { get; init; }
}

