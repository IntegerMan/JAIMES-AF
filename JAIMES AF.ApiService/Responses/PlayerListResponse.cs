namespace MattEland.Jaimes.ApiService.Responses;

public record PlayerListResponse
{
 public required PlayerInfoResponse[] Players { get; init; }
}