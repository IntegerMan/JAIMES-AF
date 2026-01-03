namespace MattEland.Jaimes.Repositories.Entities;

/// <summary>
/// Represents a test case that references a player message for agent evaluation.
/// When running tests, agents receive the last 5 messages prior to this message
/// plus the agent's prompt as the system prompt.
/// </summary>
public class TestCase
{
    public int Id { get; set; }

    /// <summary>
    /// The player message that defines this test case (the input we want to test agent responses against).
    /// </summary>
    public int MessageId { get; set; }

    public Message? Message { get; set; }

    /// <summary>
    /// User-friendly name for this test case.
    /// </summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    /// <summary>
    /// Optional notes or description about what this test case is testing.
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Soft delete support - inactive test cases are excluded from test runs by default.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<TestCaseRun> Runs { get; set; } = [];
}
