namespace MattEland.Jaimes.Repositories.Entities;

public class AgentInstructionVersion
{
    public int Id { get; set; }
    public required string AgentId { get; set; }
    public required string VersionNumber { get; set; }
    public required string Instructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    
    public Agent? Agent { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<ScenarioAgent> ScenarioAgents { get; set; } = new List<ScenarioAgent>();
}
