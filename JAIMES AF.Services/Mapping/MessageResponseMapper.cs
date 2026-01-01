namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageResponseMapper
{
    [MapProperty(nameof(MessageDto.Id), nameof(MessageResponse.Id))]
    [MapProperty(nameof(MessageDto.ParticipantName), nameof(MessageResponse.ParticipantName))]
    [MapProperty(nameof(MessageDto.PlayerId), nameof(MessageResponse.PlayerId))]
    [MapProperty(nameof(MessageDto.Text), nameof(MessageResponse.Text))]
    [MapProperty(nameof(MessageDto.CreatedAt), nameof(MessageResponse.CreatedAt))]
    [MapProperty(nameof(MessageDto.AgentId), nameof(MessageResponse.AgentId))]
    [MapProperty(nameof(MessageDto.AgentName), nameof(MessageResponse.AgentName))]
    [MapProperty(nameof(MessageDto.InstructionVersionId), nameof(MessageResponse.InstructionVersionId))]
    [MapProperty(nameof(MessageDto.IsScriptedMessage), nameof(MessageResponse.IsScriptedMessage))]
    [MapProperty(nameof(MessageDto.Sentiment), nameof(MessageResponse.Sentiment))]
    [MapProperty(nameof(MessageDto.SentimentConfidence), nameof(MessageResponse.SentimentConfidence))]
    [MapProperty(nameof(MessageDto.SentimentSource), nameof(MessageResponse.SentimentSource))]
    [MapProperty(nameof(MessageDto.SentimentId), nameof(MessageResponse.SentimentId))]
    [MapProperty(nameof(MessageDto.ModelName), nameof(MessageResponse.ModelName))]
    [MapProperty(nameof(MessageDto.ModelProvider), nameof(MessageResponse.ModelProvider))]
    [MapProperty(nameof(MessageDto.ModelEndpoint), nameof(MessageResponse.ModelEndpoint))]
    [MapProperty(nameof(MessageDto.HasMissingEvaluators), nameof(MessageResponse.HasMissingEvaluators))]
    [MapProperty(nameof(MessageDto.VersionNumber), nameof(MessageResponse.VersionNumber))]
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