using System.ComponentModel.DataAnnotations;

namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to create a test case from a player message.
/// </summary>
public record CreateTestCaseRequest
{
    /// <summary>
    /// The ID of the player message to create a test case from.
    /// </summary>
    public required int MessageId { get; init; }

    /// <summary>
    /// A user-friendly name for the test case.
    /// </summary>
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>
    /// Optional description or notes about what this test case is testing.
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; init; }
}
