using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using System.Text.RegularExpressions;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Parameter]
    public Guid GameId { get; set; }

    private List<MessageResponse> messages = [];

    private GameStateResponse? game;
    private bool isLoading = true;
    private string? errorMessage;

    private readonly MessageResponse userMessage = new()
    {
        Participant = ChatParticipant.Player,
        ParticipantName = "Player Character",
        PlayerId = null,
        Text = string.Empty,
        CreatedAt = DateTime.UtcNow
    };

    private bool isSending = false;

    protected override async Task OnParametersSetAsync()
    {
        await LoadGameAsync();
    }

    private async Task LoadGameAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            game = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
            messages = game?.Messages.ToList() ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to load game from API");
            errorMessage = "Failed to load game: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SendMessageAsync()
    {
        string message = userMessage.Text;
        userMessage.Text = string.Empty;
        await SendMessagePrivateAsync(message);
    }

    private async Task SendMessagePrivateAsync(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || isSending) return;

        isSending = true;
        errorMessage = null;
        try
        {
            // Indicate message is being sent
            ChatRequest request = new()
            {
                GameId = GameId, 
                Message = messageText
            };
            messages.Add(new MessageResponse
            {
                Text = messageText,
                Participant = ChatParticipant.Player,
                PlayerId = game!.PlayerId,
                ParticipantName = game.PlayerName,
                CreatedAt = DateTime.UtcNow
            });
            await InvokeAsync(StateHasChanged);

            // Send message to API
            HttpResponseMessage resp = await Http.PostAsJsonAsync($"/games/{GameId}", request);

            // Handle the response
            if (!resp.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(resp);
            }
            else
            {
                GameStateResponse? updated = await resp.Content.ReadFromJsonAsync<GameStateResponse>();
                messages.AddRange(updated?.Messages ?? []);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to send chat message");
            errorMessage = $"Failed to send message: {ex.Message}";
        }
        finally
        {
            isSending = false;
            StateHasChanged();
        }
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage resp)
    {
        // Try to read response body for more details
        string? body = null;
        try
        {
            body = await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            // ignored
        }

        errorMessage = $"Failed to send message: {resp.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
    }

    private async Task OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && !isSending)
        {
            await SendMessageAsync();
        }
    }

    // Computes up to two-letter initials for an avatar based on a participant name.
    // Examples: "Game Master" -> "GM", "Player Character" -> "PC", "Emie von Laurentz" -> "EL", "Madonna" -> "MA"
    private static string GetAvatarInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";

        // Normalize whitespace and split into parts
        var parts = Regex.Split(name.Trim(), "\\s+")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        // Common particles to ignore when choosing initials (e.g., 'von' in names)
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "von", "van", "de", "la", "le", "da", "di", "del", "des", "der", "den", "the", "and", "of"
        };

        var meaningful = parts.Where(p => !ignore.Contains(p)).ToList();
        if (!meaningful.Any()) meaningful = parts;

        // If there's only one meaningful part, use up to first two letters
        if (meaningful.Count ==1)
        {
            var token = Regex.Replace(meaningful[0], "[^\\p{L}]", ""); // keep letters only
            if (string.IsNullOrEmpty(token)) return "?";
            token = token.ToUpperInvariant();
            return token.Length ==1 ? token : token.Substring(0, Math.Min(2, token.Length));
        }

        // Use first letter of first and last meaningful parts
        char first = meaningful.First()[0];
        char last = meaningful.Last()[0];
        return string.Concat(char.ToUpperInvariant(first), char.ToUpperInvariant(last));
    }
}
