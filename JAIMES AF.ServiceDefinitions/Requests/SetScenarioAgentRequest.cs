namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record SetScenarioAgentRequest
{
    public required string AgentId { get; init; }
    public required int InstructionVersionId { get; init; }
}
