using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public AgentThread Thread { get; set; } = null!;
    [Inject] public AIAgent Agent { get; set; } = null!;
    
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter] public Guid GameId { get; set; }

    private List<ChatMessage> _messages = [];

    private GameStateResponse? _game;
    private bool _isLoading = true;
    private string? _errorMessage;

    private readonly MessageResponse _userMessage = new()
    {
        Participant = ChatParticipant.Player,
        ParticipantName = "Player Character",
        PlayerId = null,
        Text = string.Empty,
        CreatedAt = DateTime.UtcNow
    };

    private bool _isSending = false;
    private bool _shouldScrollToBottom = false;

    protected override async Task OnParametersSetAsync()
    {
        await LoadGameAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldScrollToBottom)
        {
            _shouldScrollToBottom = false;
            // Use a small delay to ensure DOM is fully updated
            await Task.Delay(50);
            await ScrollToBottomAsync();
        }
    }

    private async Task LoadGameAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _game = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
            _messages = [];// _game?.Messages.OrderBy(m => m.Id).ToList() ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to load game from API");
            _errorMessage = "Failed to load game: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            // Scroll to bottom after initial load
            if (_messages.Count > 0) _shouldScrollToBottom = true;
            StateHasChanged();
        }
    }

    private async Task SendMessageAsync()
    {
        string message = _userMessage.Text;
        _userMessage.Text = string.Empty;
        await SendMessagePrivateAsync(message);
    }

    private async Task SendMessagePrivateAsync(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || _isSending) return;

        _isSending = true;
        _errorMessage = null;
        try
        {
            // Indicate message is being sent
            ChatRequest request = new()
            {
                GameId = GameId,
                Message = messageText
            };
            _messages.Add(new(ChatRole.User, messageText));
            // Scroll to bottom after adding user message and showing typing indicator
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);

            // Send message to API
            AgentRunResponse response = await Agent.RunAsync(_messages, Thread);
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
                    _messages = [];// updated.Messages.OrderBy(m => m.Id).ToList();
                    // Scroll to bottom after receiving reply
                    _shouldScrollToBottom = true;
                    StateHasChanged();
                }
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to send chat message");
            _errorMessage = $"Failed to send message: {ex.Message}";
        }
        finally
        {
            _isSending = false;
            // Scroll after typing indicator disappears
            _shouldScrollToBottom = true;
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

        _errorMessage =
            $"Failed to send message: {resp.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
    }

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && !_isSending) await SendMessageAsync();
    }

    private string GetGameMasterName()
    {
        return "Game Master";
        /*
        MessageResponse? gameMasterMessage = _messages.FirstOrDefault(m => m.Participant == ChatParticipant.GameMaster);
        return gameMasterMessage?.ParticipantName ?? "Game Master";
        */
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("scrollToBottom", "chat-scroll-container");
        }
        catch (Exception)
        {
            // Ignore exceptions - element might not be rendered yet or JS interop might not be available
        }
    }
}