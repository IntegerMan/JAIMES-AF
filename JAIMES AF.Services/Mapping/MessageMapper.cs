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
    [MapperIgnoreSource(nameof(Message.GameId))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.Agent))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.MessageSentiment))]
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
}