using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageFeedbackMapper
{
    [MapperIgnoreSource(nameof(MessageFeedback.Message))]
    [MapperIgnoreSource(nameof(MessageFeedback.InstructionVersion))]
    public static partial MessageFeedbackDto ToDto(this MessageFeedback messageFeedback);

    public static partial MessageFeedbackDto[] ToDto(this IEnumerable<MessageFeedback> messageFeedbacks);
}

