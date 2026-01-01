namespace MattEland.Jaimes.Repositories.Entities;

public class ScenarioAgent
{
    public required string ScenarioId { get; set; }
    public required string AgentId { get; set; }
    public required int InstructionVersionId { get; set; }
    
    public Scenario? Scenario { get; set; }
    public Agent? Agent { get; set; }
    public AgentInstructionVersion? InstructionVersion { get; set; }
}


