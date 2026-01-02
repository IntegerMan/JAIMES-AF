using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;

namespace MattEland.Jaimes.ServiceLayer.Mapping;

[Mapper]
public static partial class MessageToolCallMapper
{
    [MapperIgnoreSource(nameof(MessageToolCall.Message))]
    [MapperIgnoreSource(nameof(MessageToolCall.InstructionVersion))]
    [MapperIgnoreSource(nameof(MessageToolCall.Tool))]
    [MapperIgnoreSource(nameof(MessageToolCall.ToolId))]
    public static partial MessageToolCallDto ToDto(this MessageToolCall messageToolCall);

    public static partial MessageToolCallDto[] ToDto(this IEnumerable<MessageToolCall> messageToolCalls);
}


