namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageMapper
{
    [MapProperty(nameof(Message.Player),
        nameof(MessageDto.ParticipantName),
        Use = nameof(MapParticipantNameFromPlayer))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelName),
        Use = nameof(MapModelNameFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelProvider),
        Use = nameof(MapModelProviderFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelEndpoint),
        Use = nameof(MapModelEndpointFromInstructionVersion))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageDto.SentimentSource),
        Use = nameof(MapSentimentSourceFromMessageSentiment))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageDto.SentimentId),
        Use = nameof(MapSentimentIdFromMessageSentiment))]
    [MapperIgnoreSource(nameof(Message.GameId))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.Agent))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.PreviousMessageId))]
    [MapperIgnoreSource(nameof(Message.PreviousMessage))]
    [MapperIgnoreSource(nameof(Message.NextMessageId))]
    [MapperIgnoreSource(nameof(Message.NextMessage))]
    [MapperIgnoreSource(nameof(Message.Model))]
    [MapperIgnoreSource(nameof(Message.ModelId))]
    [MapperIgnoreSource(nameof(Message.ToolCalls))]
    public static partial MessageDto ToDto(this Message message);

    public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);

    private static string MapParticipantNameFromPlayer(Player? player)
    {
        return player?.Name ?? "Game Master";
    }

    private static string? MapModelNameFromInstructionVersion(AgentInstructionVersion? instructionVersion)
    {
        return instructionVersion?.Model?.Name;
    }

    private static string? MapModelProviderFromInstructionVersion(AgentInstructionVersion? instructionVersion)
    {
        return instructionVersion?.Model?.Provider;
    }

    private static string? MapModelEndpointFromInstructionVersion(AgentInstructionVersion? instructionVersion)
    {
        return instructionVersion?.Model?.Endpoint;
    }

    private static int? MapSentimentSourceFromMessageSentiment(MessageSentiment? messageSentiment)
    {
        return messageSentiment != null ? (int)messageSentiment.SentimentSource : null;
    }

    private static int? MapSentimentIdFromMessageSentiment(MessageSentiment? messageSentiment)
    {
        return messageSentiment?.Id;
    }

    [MapProperty(nameof(Message.ToolCalls), nameof(MessageContextDto.ToolCalls))]
    [MapProperty(nameof(Message.MessageSentiment), nameof(MessageContextDto.SentimentSource),
        Use = nameof(MapSentimentSourceFromMessageSentiment))]
    [MapProperty(nameof(Message.MessageSentiment), nameof(MessageContextDto.SentimentId),
        Use = nameof(MapSentimentIdFromMessageSentiment))]
    [MapProperty(nameof(Message.InstructionVersion), nameof(MessageDto.ModelName),
        Use = nameof(MapModelNameFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion), nameof(MessageDto.ModelProvider),
        Use = nameof(MapModelProviderFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion), nameof(MessageDto.ModelEndpoint),
        Use = nameof(MapModelEndpointFromInstructionVersion))]
    [MapProperty(nameof(Message.Player), nameof(MessageDto.ParticipantName),
        Use = nameof(MapParticipantNameFromPlayer))]
    [MapperIgnoreTarget(nameof(MessageContextDto.Metrics))]
    [MapperIgnoreTarget(nameof(MessageContextDto.Feedback))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.Agent))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.PreviousMessageId))]
    [MapperIgnoreSource(nameof(Message.PreviousMessage))]
    [MapperIgnoreSource(nameof(Message.NextMessageId))]
    [MapperIgnoreSource(nameof(Message.NextMessage))]
    [MapperIgnoreSource(nameof(Message.Model))]
    [MapperIgnoreSource(nameof(Message.ModelId))]
    public static partial MessageContextDto ToContextDto(this Message message);

    private static MessageFeedbackResponse? MapFeedbackFromCollection(ICollection<MessageFeedback> feedbacks)
    {
        var feedback = feedbacks.FirstOrDefault();
        return feedback == null ? null : ToResponse(feedback);
    }

    [MapperIgnoreSource(nameof(MessageEvaluationMetric.EvaluationModel))]
    [MapperIgnoreSource(nameof(MessageEvaluationMetric.Message))]
    public static partial MessageEvaluationMetricResponse ToResponse(MessageEvaluationMetric metric);

    [MapperIgnoreSource(nameof(MessageFeedback.Message))]
    [MapperIgnoreSource(nameof(MessageFeedback.InstructionVersion))]
    public static partial MessageFeedbackResponse ToResponse(MessageFeedback feedback);

    [MapperIgnoreSource(nameof(MessageToolCall.Message))]
    [MapperIgnoreSource(nameof(MessageToolCall.InstructionVersion))]
    private static partial MessageToolCallResponse ToResponse(MessageToolCall toolCall);
}