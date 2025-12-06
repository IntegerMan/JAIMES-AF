namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides information about the player character from the Game context.
/// </summary>
public class PlayerInfoTool(GameDto game)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    /// <summary>
    /// Gets information about the player character for the current game.
    /// This method can be called by the AI agent to retrieve player information.
    /// </summary>
    /// <returns>A string containing the player's name, ID, and description (if available).</returns>
    [Description(
        "Retrieves detailed information about the current player character in the game, including their name, unique identifier, and character description. Use this tool whenever you need to reference or describe the player character, their background, or their current state in the game world.")]
    public string GetPlayerInfo()
    {
        string info = $"Player Name: {_game.Player.Name}\nPlayer ID: {_game.Player.Id}";

        if (!string.IsNullOrWhiteSpace(_game.Player.Description))
            info += $"\nPlayer Description: {_game.Player.Description}";

        return info;
    }
}