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
    private ILogger? _logger;

    protected override async Task OnParametersSetAsync()
    {
        await LoadGameAsync();
        _logger = LoggerFactory.CreateLogger("GameDetails");
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
            _messages = _game?.Messages.OrderBy(m => m.Id)
                .Select(m => new ChatMessage(m.Participant == ChatParticipant.Player ? ChatRole.User : ChatRole.Assistant, m.Text))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load game from API");
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
            _messages.Add(new(ChatRole.User, messageText));
            _logger?.LogInformation("Sending message {Text} from User", messageText);

            // Scroll to bottom after adding user message and showing typing indicator
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);

            // Send message to API
            AgentRunResponse resp = await Agent.RunAsync(_messages, Thread);
            foreach (var message in resp.Messages)
            {
                _logger?.LogInformation("Received message {Text} from {Role}", message.Text, message.AuthorName ?? message.Role.ToString());
                _messages.Add(message);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send chat message");
            _errorMessage = $"Failed to send message: {ex.Message}";
        }
        finally
        {
            _isSending = false;

            // Scroll after typing indicator disappears
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);
        }
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