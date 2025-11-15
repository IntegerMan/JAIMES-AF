using System.ComponentModel;
using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides information about the player character from the Game context.
/// </summary>
public class PlayerInfoTool
{
    private readonly GameDto _game;

    public PlayerInfoTool(GameDto game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
    }

    /// <summary>
    /// Gets information about the player character for the current game.
    /// This method can be called by the AI agent to retrieve player information.
    /// </summary>
    /// <returns>A string containing the player's name, ID, and description (if available).</returns>
    [Description("Gets information about the player character, including name, ID, and description.")]
    public string GetPlayerInfo()
    {
        string info = $"Player Name: {_game.Player.Name}\nPlayer ID: {_game.Player.Id}";
        
        if (!string.IsNullOrWhiteSpace(_game.Player.Description))
        {
            info += $"\nPlayer Description: {_game.Player.Description}";
        }
        
        return info;
    }
}

