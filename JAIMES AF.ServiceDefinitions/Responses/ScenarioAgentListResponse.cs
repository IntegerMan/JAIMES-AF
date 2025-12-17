namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ScenarioAgentListResponse
{
    public required ScenarioAgentResponse[] ScenarioAgents { get; init; }
}
