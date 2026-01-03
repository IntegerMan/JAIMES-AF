namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to update a test case's name and description.
/// </summary>
public class UpdateTestCaseRequest
{
    /// <summary>
    /// The new name/title for the test case.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description or notes about what this test case is testing.
    /// </summary>
    public string? Description { get; init; }
}
