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
    public static partial MessageDto ToDto(this Message message);

    public static partial MessageDto[] ToDto(this IEnumerable<Message> messages);

    private static string MapParticipantNameFromPlayer(Player? player)
    {
        return player?.Name ?? "Game Master";
    }
}