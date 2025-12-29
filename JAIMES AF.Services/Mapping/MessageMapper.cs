namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageMapper
{
    [MapProperty(nameof(Message.Player),
        nameof(MessageDto.ParticipantName),
        Use = nameof(MapParticipantNameFromPlayer))]
    [MapperIgnoreSource(nameof(Message.GameId))]
    [MapperIgnoreSource(nameof(Message.Game))]
    [MapperIgnoreSource(nameof(Message.ChatHistoryId))]
    [MapperIgnoreSource(nameof(Message.ChatHistory))]
    [MapperIgnoreSource(nameof(Message.Agent))]
    [MapperIgnoreSource(nameof(Message.InstructionVersion))]
    [MapperIgnoreSource(nameof(Message.MessageEmbedding))]
    [MapperIgnoreSource(nameof(Message.PreviousMessageId))]
    [MapperIgnoreSource(nameof(Message.PreviousMessage))]
    [MapperIgnoreSource(nameof(Message.NextMessageId))]
    [MapperIgnoreSource(nameof(Message.NextMessage))]
    public static partial MessageDto ToDto(this Message message);

    public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);

    private static string MapParticipantNameFromPlayer(Player? player)
    {
        return player?.Name ?? "Game Master";
    }
}