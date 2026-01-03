using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

public class AgentInstructionVersion
{
    public int Id { get; set; }
    public required string AgentId { get; set; }
    public required string VersionNumber { get; set; }
    public required string Instructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the ID of the model used with this instruction version.
    /// </summary>
    public int? ModelId { get; set; }

    /// <summary>
    /// Navigation property to the model used with this instruction version.
    /// </summary>
    [ForeignKey(nameof(ModelId))]
    public Model? Model { get; set; }
    
    public Agent? Agent { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<ScenarioAgent> ScenarioAgents { get; set; } = new List<ScenarioAgent>();
}



