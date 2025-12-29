using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails : IDisposable
{
    [Inject] public IHttpClientFactory HttpClientFactory { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] public IDialogService DialogService { get; set; } = null!;

    [Parameter] public Guid GameId { get; set; }

    private List<ChatMessage> _messages = [];
    private List<int?> _messageIds = []; // Parallel list to track message IDs
    private Dictionary<int, MessageFeedbackInfo> _messageFeedback = new();
    private Dictionary<int, List<MessageToolCallInfo>> _messageToolCalls = new();
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

    // Hover state tracking for feedback buttons
    private int? _hoveredMessageId;

    private bool IsHovering => _hoveredMessageId.HasValue;

    public record MessageFeedbackInfo
    {
        public required int MessageId { get; init; }
        public required bool IsPositive { get; init; }
        public string? Comment { get; init; }
    }

    public record MessageToolCallInfo
    {
        public required int Id { get; init; }
        public required int MessageId { get; init; }
        public required string ToolName { get; init; }
        public string? InputJson { get; init; }
        public string? OutputJson { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

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
            
            // Build messages and track IDs
            var orderedMessages = _game?.Messages.OrderBy(m => m.Id).ToList() ?? [];
            _messages = orderedMessages
                .Select(m => new ChatMessage(m.Participant == ChatParticipant.Player ? ChatRole.User : ChatRole.Assistant, m.Text))
                .ToList();
            _messageIds = orderedMessages.Select(m => (int?)m.Id).ToList();

            // Load existing feedback for assistant messages
            await LoadFeedbackForMessagesAsync(orderedMessages.Where(m => m.Participant == ChatParticipant.GameMaster).Select(m => m.Id).ToList());

            // Load tool calls for assistant messages
            await LoadToolCallsForMessagesAsync(orderedMessages.Where(m => m.Participant == ChatParticipant.GameMaster).Select(m => m.Id).ToList());

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
                // Note: New messages from AGUI don't have database IDs yet, so we track as null
                // They will get IDs when saved to the database, but we can't provide feedback until they're saved
                _messageIds.Add(null);
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

    /// <summary>
    /// Converts a UTC DateTime to local time for display.
    /// </summary>
    private DateTime ToLocalTime(DateTime utcTime)
    {
        // Ensure the DateTime is treated as UTC, then convert to local time
        if (utcTime.Kind == DateTimeKind.Unspecified)
        {
            utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        }
        return utcTime.ToLocalTime();
    }

    /// <summary>
    /// Loads existing feedback for the specified message IDs.
    /// </summary>
    private async Task LoadFeedbackForMessagesAsync(List<int> messageIds)
    {
        if (messageIds.Count == 0) return;

        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            
            // Load feedback for each message (could be optimized with a batch endpoint, but this works for now)
            foreach (int messageId in messageIds)
            {
                try
                {
                    MessageFeedbackResponse? feedback = await httpClient.GetFromJsonAsync<MessageFeedbackResponse>($"/messages/{messageId}/feedback");
                    if (feedback != null)
                    {
                        _messageFeedback[messageId] = new MessageFeedbackInfo
                        {
                            MessageId = feedback.MessageId,
                            IsPositive = feedback.IsPositive,
                            Comment = feedback.Comment
                        };
                    }
                }
                catch (HttpRequestException)
                {
                    // Message doesn't have feedback yet, which is fine
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load feedback for messages");
        }
    }

    /// <summary>
    /// Gets the message ID for a message at the specified index, if available.
    /// </summary>
    private int? GetMessageId(int index)
    {
        if (index >= 0 && index < _messageIds.Count)
            return _messageIds[index];
        return null;
    }

    /// <summary>
    /// Handles hover start for a message bubble.
    /// </summary>
    private void HoverStart(int? messageId)
    {
        if (messageId.HasValue)
        {
            _hoveredMessageId = messageId;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles hover stop for a message bubble.
    /// </summary>
    private void HoverStop(int? messageId)
    {
        if (_hoveredMessageId == messageId)
        {
            _hoveredMessageId = null;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Shows the feedback dialog and submits feedback for a message.
    /// </summary>
    private async Task ShowFeedbackDialogAsync(int messageId, bool? isPositive = null)
    {
        // Stop hovering when dialog opens
        _hoveredMessageId = null;
        StateHasChanged();

        // Check if feedback already exists
        if (_messageFeedback.ContainsKey(messageId))
        {
            // Show existing feedback info
            MessageFeedbackInfo existing = _messageFeedback[messageId];
            await DialogService.ShowMessageBox(
                "Feedback Already Submitted",
                $"You have already submitted {(existing.IsPositive ? "positive" : "negative")} feedback for this message." +
                (string.IsNullOrWhiteSpace(existing.Comment) ? "" : $"\n\nYour comment: {existing.Comment}"),
                "OK");
            return;
        }

        // Show feedback dialog
        // If isPositive is provided (direct click from popover), pre-select it in the dialog
        IDialogReference? dialogRef = null;
        var parameters = new DialogParameters<FeedbackDialog>
        {
            { nameof(FeedbackDialog.MessageId), messageId },
            { nameof(FeedbackDialog.PreSelectedFeedback), isPositive },
            { nameof(FeedbackDialog.OnFeedbackSubmitted), EventCallback.Factory.Create<MessageFeedbackInfo?>(this, async (MessageFeedbackInfo? feedback) =>
            {
                if (feedback != null)
                {
                    await SubmitFeedbackAsync(feedback.MessageId, feedback.IsPositive, feedback.Comment);
                    // Close the dialog after submitting
                    if (dialogRef != null)
                    {
                        dialogRef.Close(DialogResult.Ok(true));
                    }
                }
            })}
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        dialogRef = await DialogService.ShowAsync<FeedbackDialog>("Provide Feedback", parameters, options);
        
        // Also handle cancellation
        var result = await dialogRef.Result;
    }

    /// <summary>
    /// Submits feedback to the API.
    /// </summary>
    private async Task SubmitFeedbackAsync(int messageId, bool isPositive, string? comment)
    {
        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            
            SubmitMessageFeedbackRequest request = new()
            {
                IsPositive = isPositive,
                Comment = comment
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync($"/messages/{messageId}/feedback", request);
            
            if (response.IsSuccessStatusCode)
            {
                MessageFeedbackResponse? feedback = await response.Content.ReadFromJsonAsync<MessageFeedbackResponse>();
                if (feedback != null)
                {
                    _messageFeedback[messageId] = new MessageFeedbackInfo
                    {
                        MessageId = feedback.MessageId,
                        IsPositive = feedback.IsPositive,
                        Comment = feedback.Comment
                    };
                    StateHasChanged();
                }
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Failed to submit feedback: {Error}", errorMessage);
                await DialogService.ShowMessageBox("Error", $"Failed to submit feedback: {errorMessage}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to submit feedback");
            await DialogService.ShowMessageBox("Error", $"Failed to submit feedback: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Loads tool calls for the specified message IDs.
    /// </summary>
    private async Task LoadToolCallsForMessagesAsync(List<int> messageIds)
    {
        if (messageIds.Count == 0) return;

        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            
            // Load tool calls for each message
            foreach (int messageId in messageIds)
            {
                try
                {
                    List<MessageToolCallResponse>? toolCalls = await httpClient.GetFromJsonAsync<List<MessageToolCallResponse>>($"/messages/{messageId}/tool-calls");
                    if (toolCalls != null && toolCalls.Count > 0)
                    {
                        _messageToolCalls[messageId] = toolCalls.Select(tc => new MessageToolCallInfo
                        {
                            Id = tc.Id,
                            MessageId = tc.MessageId,
                            ToolName = tc.ToolName,
                            InputJson = tc.InputJson,
                            OutputJson = tc.OutputJson,
                            CreatedAt = tc.CreatedAt
                        }).ToList();
                    }
                }
                catch (HttpRequestException)
                {
                    // Message doesn't have tool calls yet, which is fine
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load tool calls for messages");
        }
    }

    /// <summary>
    /// Shows the tool calls dialog for a message.
    /// </summary>
    private async Task ShowToolCallsDialogAsync(int messageId)
    {
        if (!_messageToolCalls.TryGetValue(messageId, out List<MessageToolCallInfo>? toolCalls) || toolCalls == null || toolCalls.Count == 0)
        {
            await DialogService.ShowMessageBox("No Tool Calls", "This message has no tool calls.", "OK");
            return;
        }

        var parameters = new DialogParameters<ToolCallsDialog>
        {
            { nameof(ToolCallsDialog.ToolCalls), toolCalls }
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Large,
            FullWidth = true
        };

        IDialogReference? dialogRef = await DialogService.ShowAsync<ToolCallsDialog>("Tool Calls", parameters, options);
        await dialogRef.Result;
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}