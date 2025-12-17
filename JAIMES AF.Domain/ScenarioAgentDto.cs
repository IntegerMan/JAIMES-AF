namespace MattEland.Jaimes.Domain;

public class ScenarioAgentDto
{
    public required string ScenarioId { get; init; }
    public required string AgentId { get; init; }
    public required int InstructionVersionId { get; init; }
}
