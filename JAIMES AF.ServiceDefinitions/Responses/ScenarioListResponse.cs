namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ScenarioListResponse
{
 public required ScenarioInfoResponse[] Scenarios { get; init; }
}
