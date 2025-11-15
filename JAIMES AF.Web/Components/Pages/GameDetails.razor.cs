using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = null!;

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
    private bool shouldScrollToBottom = false;

    protected override async Task OnParametersSetAsync()
    {
        await LoadGameAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (shouldScrollToBottom)
        {
            shouldScrollToBottom = false;
            // Use a small delay to ensure DOM is fully updated
            await Task.Delay(50);
            await ScrollToBottomAsync();
        }
    }

    private async Task LoadGameAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            game = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
            messages = (game?.Messages.OrderBy(m => m.Id).ToList()) ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to load game from API");
            errorMessage = "Failed to load game: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            // Scroll to bottom after initial load
            if (messages.Count > 0)
            {
                shouldScrollToBottom = true;
            }
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
                Id = 0, // Temporary ID, will be replaced when we reload or get proper ordering
                Text = messageText,
                Participant = ChatParticipant.Player,
                PlayerId = game!.PlayerId,
                ParticipantName = game.PlayerName,
                CreatedAt = DateTime.UtcNow
            });
            // Scroll to bottom after adding user message and showing typing indicator
            shouldScrollToBottom = true;
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
                // Reload all messages from the server to get correct Ids for all messages
                // This ensures the player message we added locally gets its proper Id from the database
                GameStateResponse? updated = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
                if (updated?.Messages != null)
                {
                    messages = updated.Messages.OrderBy(m => m.Id).ToList();
                    // Scroll to bottom after receiving reply
                    shouldScrollToBottom = true;
                    StateHasChanged();
                }
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
            // Scroll after typing indicator disappears
            shouldScrollToBottom = true;
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

    private string GetGameMasterName()
    {
        MessageResponse? gameMasterMessage = messages.FirstOrDefault(m => m.Participant == ChatParticipant.GameMaster);
        return gameMasterMessage?.ParticipantName ?? "Game Master";
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("scrollToBottom", "chat-scroll-container");
        }
        catch (Exception)
        {
            // Ignore exceptions - element might not be rendered yet or JS interop might not be available
        }
    }
}
