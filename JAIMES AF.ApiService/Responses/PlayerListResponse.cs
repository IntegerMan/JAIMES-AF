namespace MattEland.Jaimes.ApiService.Responses;

public record PlayerListResponse
{
 public required PlayerInfoResponse[] Players { get; init; }
}

public record PlayerInfoResponse
{
 public required string Id { get; init; }
 public required string RulesetId { get; init; }
 public string? Description { get; init; }
}
