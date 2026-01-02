namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public record SetScenarioAgentRequest
{
    public required string AgentId { get; init; }
    public int? InstructionVersionId { get; init; }
}



