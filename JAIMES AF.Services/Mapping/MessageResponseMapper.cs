namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageResponseMapper
{
    [MapProperty(nameof(MessageDto.Id), nameof(MessageResponse.Id))]
    [MapProperty(nameof(MessageDto.ParticipantName), nameof(MessageResponse.ParticipantName))]
    [MapProperty(nameof(MessageDto.PlayerId), nameof(MessageResponse.PlayerId))]
    [MapProperty(nameof(MessageDto.Text), nameof(MessageResponse.Text))]
    [MapProperty(nameof(MessageDto.CreatedAt), nameof(MessageResponse.CreatedAt))]
    [MapProperty(nameof(MessageDto.PlayerId),
        nameof(MessageResponse.Participant),
        Use = nameof(MapParticipantFromPlayerId))]
    public static partial MessageResponse ToResponse(this MessageDto message);

    public static partial MessageResponse[] ToResponse(this IEnumerable<MessageDto> messages);

    private static ChatParticipant MapParticipantFromPlayerId(string? playerId)
    {
        return string.IsNullOrEmpty(playerId) ? ChatParticipant.GameMaster : ChatParticipant.Player;
    }
}