namespace MattEland.Jaimes.Web.Components.Chat;

/// <summary>
/// Position for chat bubbles, replacing MudBlazor's ChatBubblePosition
/// </summary>
public enum ChatBubblePosition
{
    /// <summary>
    /// Bubble appears on the left (typically for received messages)
    /// </summary>
    Start,

    /// <summary>
    /// Bubble appears on the right (typically for sent messages)
    /// </summary>
    End
}
