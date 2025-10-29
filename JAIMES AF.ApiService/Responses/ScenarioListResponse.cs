namespace MattEland.Jaimes.ApiService.Responses;

public record ScenarioListResponse
{
    public required ScenarioInfoResponse[] Scenarios { get; init; }
}