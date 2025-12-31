using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public class MessageContextDto : MessageDto
{
    public Guid GameId { get; set; }
    public List<MessageEvaluationMetricResponse> Metrics { get; set; } = [];
    public List<MessageToolCallResponse> ToolCalls { get; set; } = [];
    public MessageFeedbackResponse? Feedback { get; set; }
}
