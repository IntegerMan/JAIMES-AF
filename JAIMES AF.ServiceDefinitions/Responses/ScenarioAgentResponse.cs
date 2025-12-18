namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record ScenarioAgentResponse
{
    public required string ScenarioId { get; init; }
    public required string AgentId { get; init; }
    public required int InstructionVersionId { get; init; }
}
