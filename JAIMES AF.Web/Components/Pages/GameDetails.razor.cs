using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using Microsoft.AspNetCore.Components.Web;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Parameter]
    public Guid GameId { get; set; }

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
        if (string.IsNullOrWhiteSpace(newMessage) || isSending)
            return;
        isSending = true;
        errorMessage = null;
        try
        {
            var request = new ChatRequest { GameId = GameId, Message = newMessage };
            var resp = await Http.PostAsJsonAsync($"/games/{GameId}", request);

            if (!resp.IsSuccessStatusCode)
            {
                // Try to read response body for more details
                string? body = null;
                try { body = await resp.Content.ReadAsStringAsync(); } catch { }
                errorMessage = $"Failed to send message: {resp.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                return;
            }

            // Read returned game state object from response and update UI directly
            GameStateResponse? updated = null;
            try
            {
                updated = await resp.Content.ReadFromJsonAsync<GameStateResponse?>();
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("GameDetails").LogWarning(ex, "Response content could not be parsed as GameStateResponse");
            }

            if (updated is not null)
            {
                game = updated;
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
            errorMessage = "Failed to send message: " + ex.Message;
        }
        finally
        {
            isSending = false;
            StateHasChanged();
        }
    }

    private async Task OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await SendMessageAsync();
        }
    }
}
