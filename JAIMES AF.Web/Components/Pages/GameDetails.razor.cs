using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Requests;

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

    // New fields for chat input
    private string newMessage = string.Empty;
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
        if (string.IsNullOrWhiteSpace(newMessage) || isSending) return;

        isSending = true;
        errorMessage = null;
        try
        {
            ChatRequest request = new()
            {
                GameId = GameId, 
                Message = newMessage
            };

            messages.Add(new MessageResponse(newMessage, ChatParticipant.Player));

            HttpResponseMessage resp = await Http.PostAsJsonAsync($"/games/{GameId}", request);

            if (!resp.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(resp);
                return;
            }

            // Read returned game state object from response and update UI directly
            GameStateResponse? updated = await resp.Content.ReadFromJsonAsync<GameStateResponse?>();

            if (updated is not null)
            {
                messages.AddRange(updated.Messages);
                newMessage = string.Empty;
            } 
            else
            {
                // Fallback: reload full game state
                await LoadGameAsync();
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
        if (args.Key == "Enter")
        {
            await SendMessageAsync();
        }
    }
}
