namespace MattEland.Jaimes.ServiceDefinitions.Requests;

public class SubmitMessageFeedbackRequest
{
    public required bool IsPositive { get; set; }
    public string? Comment { get; set; }
}

