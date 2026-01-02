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
    [MapProperty("InstructionVersion.VersionNumber", nameof(MessageDto.VersionNumber))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageDto.SentimentSource),
        Use = nameof(MapSentimentSourceFromMessageSentiment))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageDto.SentimentId),
        Use = nameof(MapSentimentIdFromMessageSentiment))]
    [MapProperty(nameof(Message.Agent),
        nameof(MessageDto.AgentName),
        Use = nameof(MapAgentNameFromAgent))]
    [MapperIgnoreSource(nameof(Message.GameId))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.PreviousMessageId))]
    [MapperIgnoreSource(nameof(Message.PreviousMessage))]
    [MapperIgnoreSource(nameof(Message.NextMessageId))]
    [MapperIgnoreSource(nameof(Message.NextMessage))]
    [MapperIgnoreSource(nameof(Message.Model))]
    [MapperIgnoreSource(nameof(Message.ModelId))]
    [MapperIgnoreSource(nameof(Message.ToolCalls))]
    [MapperIgnoreTarget(nameof(MessageDto.HasMissingEvaluators))]
    [MapProperty(nameof(Message.TestCase),
        nameof(MessageDto.IsTestCase),
        Use = nameof(MapIsTestCaseFromTestCase))]
    [MapProperty(nameof(Message.TestCase),
        nameof(MessageDto.TestCaseId),
        Use = nameof(MapTestCaseIdFromTestCase))]
    public static partial MessageDto ToDto(this Message message);

    public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);

    private static bool MapIsTestCaseFromTestCase(TestCase? testCase)
    {
        return testCase != null && testCase.IsActive;
    }

    private static int? MapTestCaseIdFromTestCase(TestCase? testCase)
    {
        return testCase?.IsActive == true ? testCase.Id : null;
    }

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

    private static string? MapAgentNameFromAgent(Agent? agent)
    {
        return agent?.Name;
    }

    [MapProperty(nameof(Message.Agent),
        nameof(MessageDto.AgentName),
        Use = nameof(MapAgentNameFromAgent))]
    [MapProperty(nameof(Message.ToolCalls), nameof(MessageContextDto.ToolCalls))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageContextDto.SentimentSource),
        Use = nameof(MapSentimentSourceFromMessageSentiment))]
    [MapProperty(nameof(Message.MessageSentiment),
        nameof(MessageContextDto.SentimentId),
        Use = nameof(MapSentimentIdFromMessageSentiment))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelName),
        Use = nameof(MapModelNameFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelProvider),
        Use = nameof(MapModelProviderFromInstructionVersion))]
    [MapProperty(nameof(Message.InstructionVersion),
        nameof(MessageDto.ModelEndpoint),
        Use = nameof(MapModelEndpointFromInstructionVersion))]
    [MapProperty("InstructionVersion.VersionNumber", nameof(MessageDto.VersionNumber))]
    [MapProperty(nameof(Message.Player),
        nameof(MessageDto.ParticipantName),
        Use = nameof(MapParticipantNameFromPlayer))]
    [MapperIgnoreTarget(nameof(MessageContextDto.GameTitle))]
    [MapperIgnoreTarget(nameof(MessageContextDto.InstructionVersionNumber))]
    [MapperIgnoreTarget(nameof(MessageContextDto.Metrics))]
    [MapperIgnoreTarget(nameof(MessageContextDto.Feedback))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.PreviousMessageId))]
    [MapperIgnoreSource(nameof(Message.PreviousMessage))]
    [MapperIgnoreSource(nameof(Message.NextMessageId))]
    [MapperIgnoreSource(nameof(Message.NextMessage))]
    [MapperIgnoreSource(nameof(Message.Model))]
    [MapperIgnoreSource(nameof(Message.ModelId))]
    [MapProperty(nameof(Message.TestCase),
        nameof(MessageContextDto.IsTestCase),
        Use = nameof(MapIsTestCaseFromTestCase))]
    [MapProperty(nameof(Message.TestCase),
        nameof(MessageContextDto.TestCaseId),
        Use = nameof(MapTestCaseIdFromTestCase))]
    [MapperIgnoreTarget(nameof(MessageContextDto.HasMissingEvaluators))]
    public static partial MessageContextDto ToContextDto(this Message message);

    private static MessageFeedbackResponse? MapFeedbackFromCollection(ICollection<MessageFeedback> feedbacks)
    {
        var feedback = feedbacks.FirstOrDefault();
        return feedback == null ? null : ToResponse(feedback);
    }

    [MapperIgnoreSource(nameof(MessageEvaluationMetric.EvaluationModel))]
    [MapperIgnoreSource(nameof(MessageEvaluationMetric.Message))]
    [MapProperty(nameof(MessageEvaluationMetric.EvaluatorId), nameof(MessageEvaluationMetricResponse.EvaluatorId))]
    [MapProperty("Evaluator.Name", nameof(MessageEvaluationMetricResponse.EvaluatorName))]
    public static partial MessageEvaluationMetricResponse ToResponse(MessageEvaluationMetric metric);

    [MapperIgnoreSource(nameof(MessageFeedback.Message))]
    [MapperIgnoreSource(nameof(MessageFeedback.InstructionVersion))]
    public static partial MessageFeedbackResponse ToResponse(MessageFeedback feedback);

    [MapperIgnoreSource(nameof(MessageToolCall.Message))]
    [MapperIgnoreSource(nameof(MessageToolCall.InstructionVersion))]
    [MapperIgnoreSource(nameof(MessageToolCall.Tool))]
    [MapperIgnoreSource(nameof(MessageToolCall.ToolId))]
    private static partial MessageToolCallResponse ToResponse(MessageToolCall toolCall);
}