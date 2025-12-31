using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a tool that the AI agent can call.
/// These are auto-detected by scanning the JAIMES AF.Tools assembly.
/// </summary>
[Table("Tools")]
public class Tool
{
    /// <summary>
    /// Gets or sets the unique identifier for this tool.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the tool (e.g., "SearchRules", "GetPlayerInfo").
    /// This is used for matching against MessageToolCall.ToolName.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable description of the tool.
    /// Captured from the [Description] attribute on the tool's methods.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the tool (e.g., "Search", "Analysis", "Player").
    /// </summary>
    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this tool was first registered in the database.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to tool calls made using this tool.
    /// </summary>
    public ICollection<MessageToolCall> ToolCalls { get; set; } = new List<MessageToolCall>();
}
