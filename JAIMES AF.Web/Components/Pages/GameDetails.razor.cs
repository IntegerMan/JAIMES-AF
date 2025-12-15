using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Inject] public IHttpClientFactory HttpClientFactory { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter] public Guid GameId { get; set; }

    private List<ChatMessage> _messages = [];
    private AIAgent? _agent;

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
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            _game = await httpClient.GetFromJsonAsync<GameStateResponse>($"/games/{GameId}");
            _messages = _game?.Messages.OrderBy(m => m.Id)
                .Select(m => new ChatMessage(m.Participant == ChatParticipant.Player ? ChatRole.User : ChatRole.Assistant, m.Text))
                .ToList() ?? [];

            // Create AG-UI client for this game
            HttpClient aguiHttpClient = HttpClientFactory.CreateClient("AGUI");
            string serverUrl = $"{aguiHttpClient.BaseAddress}games/{GameId}/chat";
            AGUIChatClient chatClient = new(aguiHttpClient, serverUrl);
            _agent = chatClient.CreateAIAgent(name: $"game-{GameId}", description: "Game Chat Agent");

            // AGUI manages threads via ConversationId automatically
            // Don't deserialize server-side threads as they use MessageStore which conflicts with AGUI's ConversationId
            // Don't create a thread here - let AGUI manage it completely via ConversationId
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
            // Check if this is the first player message (no User messages yet)
            bool isFirstPlayerMessage = IsFirstPlayerMessage();

            // Indicate message is being sent
            _messages.Add(new(ChatRole.User, messageText));
            _logger?.LogInformation("Sending message {Text} from User (first message: {IsFirst})", messageText, isFirstPlayerMessage);

            // Scroll to bottom after adding user message and showing typing indicator
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);

            // Send message to API
            if (_agent == null)
            {
                _errorMessage = "Agent not initialized";
                return;
            }

            // Build the messages to send to the agent
            // For the first message, send TWO messages: the initial greeting as Assistant, then the player's response as User
            // For subsequent messages, just send the player's new message
            List<ChatMessage> messagesToSend = [];

            if (isFirstPlayerMessage)
            {
                string? initialGreeting = GetInitialGreeting();
                if (!string.IsNullOrWhiteSpace(initialGreeting))
                {
                    // First, add the initial greeting as an Assistant message so the agent knows what was displayed
                    messagesToSend.Add(new ChatMessage(ChatRole.Assistant, initialGreeting));
                    _logger?.LogInformation("First player message - including initial greeting as Assistant message for agent context");
                }
            }

            // Add the player's message
            messagesToSend.Add(new ChatMessage(ChatRole.User, messageText));

            // AGUI manages threads automatically via ConversationId
            // Don't pass a thread - AGUI will create/manage it via ConversationId to avoid MessageStore conflicts
            // AGUI manages conversation history, so we only need to send the NEW message(s), not all messages
            // Sending all messages causes exponential growth because the server thread already contains history
            _logger?.LogDebug("Sending {Count} message(s) to AGUI (AGUI manages conversation history via ConversationId)", messagesToSend.Count);
            AgentRunResponse resp = await _agent.RunAsync(messagesToSend, thread: null);

            // Only add valid messages with proper roles to the collection
            foreach (var message in resp.Messages ?? [])
            {
                // Skip null messages
                if (message == null)
                {
                    _logger?.LogWarning("Skipping null message in response");
                    continue;
                }

                // Skip messages with empty text
                if (string.IsNullOrWhiteSpace(message.Text))
                {
                    _logger?.LogDebug("Skipping message with empty text, Role: {Role}", message.Role);
                    continue;
                }

                // Normalize and validate the role
                // AGUI may return roles as strings or with different casing
                // Handle potential issues with Role enum
                ChatRole messageRole = message.Role;
                string roleString = messageRole.ToString()?.Trim() ?? string.Empty;

                // Handle case-insensitive role matching and empty/invalid roles
                ChatRole normalizedRole;
                if (string.IsNullOrEmpty(roleString) ||
                    (!roleString.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                     !roleString.Equals("Assistant", StringComparison.OrdinalIgnoreCase)))
                {
                    // Try to infer role from AuthorName or default to Assistant for non-User messages
                    if (!string.IsNullOrWhiteSpace(message.AuthorName))
                    {
                        _logger?.LogDebug("Message has invalid role '{Role}' but AuthorName '{AuthorName}', inferring Assistant role", roleString, message.AuthorName);
                        normalizedRole = ChatRole.Assistant;
                    }
                    else
                    {
                        _logger?.LogWarning("Skipping message with invalid role: '{Role}', Text: '{Text}'", roleString, message.Text);
                        continue;
                    }
                }
                else
                {
                    // Normalize the role to proper enum value
                    normalizedRole = roleString.Equals("User", StringComparison.OrdinalIgnoreCase)
                        ? ChatRole.User
                        : ChatRole.Assistant;
                }

                // Create a new message with normalized role to ensure consistency
                ChatMessage normalizedMessage = new(normalizedRole, message.Text)
                {
                    AuthorName = message.AuthorName
                };

                _logger?.LogInformation("Received message '{Text}' from {Role}", message.Text, normalizedRole);
                _messages.Add(normalizedMessage);
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

    /// <summary>
    /// Checks if the player has not yet sent any messages in this game.
    /// This is used to determine whether to include the initial greeting context
    /// when sending the first message to the agent.
    /// </summary>
    private bool IsFirstPlayerMessage()
    {
        // Check if there are any User (player) messages in the current message list
        // Note: We check before adding the new message, so if there are no User messages,
        // this is the first player message
        return !_messages.Any(m => m.Role == ChatRole.User);
    }

    /// <summary>
    /// Gets the initial greeting message from the Game Master.
    /// This is the first Assistant message in the conversation, which was displayed
    /// to the player when the game started.
    /// </summary>
    private string? GetInitialGreeting()
    {
        // The initial greeting is the first Assistant message (from Game Master)
        return _messages
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => m.Text)
            .FirstOrDefault();
    }
}