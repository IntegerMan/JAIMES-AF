namespace MattEland.Jaimes.Repositories.Entities;

public class Agent
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Role { get; set; }
    
    public ICollection<AgentInstructionVersion> InstructionVersions { get; set; } = new List<AgentInstructionVersion>();
    public ICollection<ScenarioAgent> ScenarioAgents { get; set; } = new List<ScenarioAgent>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}


