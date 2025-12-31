using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.AI;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails : IAsyncDisposable
{
    [Inject] public IHttpClientFactory HttpClientFactory { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] public IDialogService DialogService { get; set; } = null!;
    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    [Parameter] public Guid GameId { get; set; }

    private List<ChatMessage> _messages = [];
    private List<int?> _messageIds = []; // Parallel list to track message IDs
    private Dictionary<int, MessageFeedbackResponse> _messageFeedback = new();
    private Dictionary<int, List<MessageToolCallResponse>> _messageToolCalls = new();
    private Dictionary<int, List<MessageEvaluationMetricResponse>> _messageMetrics = new();
    private Dictionary<int, MessageSentimentInfo> _messageSentiment = new();
    private Dictionary<int, MessageAgentInfo> _messageAgentInfo = new();
    private AIAgent? _agent;

    private GameStateResponse? _game;
    private bool _isLoading = true;
    private string? _errorMessage;
    private int? _failedMessageIndex;
    private HubConnection? _hubConnection;

    // Title editing
    private string? _editableTitle;
    private bool _isSavingTitle = false;
    private bool _isEditingTitle = false;


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
    private int? _hoveredMessageIndex; // For messages without IDs yet

    private bool IsHovering => _hoveredMessageId.HasValue || _hoveredMessageIndex.HasValue;


    protected override async Task OnParametersSetAsync()
    {
        await LoadGameAsync();
        _logger = LoggerFactory.CreateLogger("GameDetails");

        // Connect to SignalR hub for real-time updates
        await ConnectToSignalRHubAsync();
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
                .Select(m =>
                    new ChatMessage(m.Participant == ChatParticipant.Player ? ChatRole.User : ChatRole.Assistant,
                        m.Text))
                .ToList();
            _messageIds = orderedMessages.Select(m => (int?) m.Id).ToList();

            // Batch load all metadata (Feedback, ToolCalls, Metrics, Sentiment)
            await LoadMessagesMetadataAsync(orderedMessages.Select(m => m.Id).ToList());

            // Track agent info per message
            _messageAgentInfo.Clear();
            foreach (var message in orderedMessages)
            {
                if ((!string.IsNullOrEmpty(message.AgentId) || message.InstructionVersionId.HasValue) &&
                    message.Participant == ChatParticipant.GameMaster)
                {
                    _messageAgentInfo[message.Id] = new MessageAgentInfo
                    {
                        AgentId = message.AgentId,
                        // We don't have the agent name in the message response directly unless we fetch it separately
                        // But we can fallback to AgentId if needed. Ideally MessageResponse should have AgentName.
                        // For now we'll do our best.
                        InstructionVersionId = message.InstructionVersionId,
                        // We don't have version number either.
                        IsScriptedMessage = message.IsScriptedMessage
                    };
                }
            }

            // Create AG-UI client for this game
            HttpClient aguiHttpClient = HttpClientFactory.CreateClient("AGUI");
            string serverUrl = $"{aguiHttpClient.BaseAddress}games/{GameId}/chat";
            AGUIChatClient chatClient = new(aguiHttpClient, serverUrl);
            _agent = chatClient.CreateAIAgent(name: $"game-{GameId}", description: "Game Chat Agent");

            // Initialize editable title
            _editableTitle = _game?.Title;

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

    /// <summary>
    /// Saves the game title via the API.
    /// </summary>
    private async Task SaveTitleAsync()
    {
        if (_game == null || _isSavingTitle) return;

        _isSavingTitle = true;
        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            var request = new UpdateGameRequest {Title = _editableTitle};
            var response = await httpClient.PutAsJsonAsync($"/games/{GameId}", request);

            if (response.IsSuccessStatusCode)
            {
                var updatedGame = await response.Content.ReadFromJsonAsync<GameInfoResponse>();
                if (updatedGame != null)
                {
                    // Update local game state with new title
                    _game = _game with {Title = updatedGame.Title};
                    UpdateBreadcrumbs(_game.Title ?? $"{_game.PlayerName} in {_game.ScenarioName}");
                }
            }
            else
            {
                _logger?.LogError("Failed to save title: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save game title");
        }
        finally
        {
            _isSavingTitle = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Enters title edit mode.
    /// </summary>
    private void EnterTitleEditMode()
    {
        _editableTitle = _game?.Title ?? $"{_game?.PlayerName} in {_game?.ScenarioName}";
        _isEditingTitle = true;
        StateHasChanged();
    }

    /// <summary>
    /// Cancels title edit mode without saving.
    /// </summary>
    private void CancelTitleEdit()
    {
        _isEditingTitle = false;
        _editableTitle = _game?.Title;
        StateHasChanged();
    }

    /// <summary>
    /// Saves the title and exits edit mode.
    /// </summary>
    private async Task SaveTitleAndExitEditModeAsync()
    {
        await SaveTitleAsync();
        _isEditingTitle = false;
        StateHasChanged();
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
        _failedMessageIndex = null;
        int currentMessageIndex = -1;

        // Check if this is the first player message (no User messages yet)
        bool isFirstPlayerMessage = IsFirstPlayerMessage();

        try
        {
            // Indicate message is being sent
            _messages.Add(new(ChatRole.User, messageText));
            _messageIds.Add(null); // User messages don't have database IDs yet
            currentMessageIndex = _messages.Count - 1;

            _logger?.LogInformation("Sending message {Text} from User (first message: {IsFirst})",
                messageText,
                isFirstPlayerMessage);

            // Scroll to bottom after adding user message and showing typing indicator
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);

            // Send message to API
            if (_agent == null)
            {
                _errorMessage = "Agent not initialized";
                _failedMessageIndex = currentMessageIndex;
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
                    _logger?.LogInformation(
                        "First player message - including initial greeting as Assistant message for agent context");
                }
            }

            // Add the player's message
            messagesToSend.Add(new ChatMessage(ChatRole.User, messageText));

            // AGUI manages threads automatically via ConversationId
            // Don't pass a thread - AGUI will create/manage it via ConversationId to avoid MessageStore conflicts
            // AGUI manages conversation history, so we only need to send the NEW message(s), not all messages
            // Sending all messages causes exponential growth because the server thread already contains history
            _logger?.LogDebug(
                "Sending {Count} message(s) to AGUI (AGUI manages conversation history via ConversationId)",
                messagesToSend.Count);
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
                        _logger?.LogDebug(
                            "Message has invalid role '{Role}' but AuthorName '{AuthorName}', inferring Assistant role",
                            roleString,
                            message.AuthorName);
                        normalizedRole = ChatRole.Assistant;
                    }
                    else
                    {
                        _logger?.LogWarning("Skipping message with invalid role: '{Role}', Text: '{Text}'",
                            roleString,
                            message.Text);
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
            _failedMessageIndex = currentMessageIndex != -1 ? currentMessageIndex : _messages.Count - 1;
        }
        finally
        {
            _isSending = false;

            // Don't reload game state here - SignalR will notify us when sentiment is analyzed
            // and we'll update the message ID at that point

            // Scroll after typing indicator disappears
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RetryMessageAsync(int messageIndex)
    {
        if (messageIndex < 0 || messageIndex >= _messages.Count) return;

        // Get the failed message text
        string messageText = _messages[messageIndex].Text;

        // Remove the failed message and any subsequent messages from the list (cleanup failed attempt)
        int countToRemove = _messages.Count - messageIndex;
        _messages.RemoveRange(messageIndex, countToRemove);
        _messageIds.RemoveRange(messageIndex, countToRemove);

        _failedMessageIndex = null;
        _errorMessage = null;

        // Resend the message
        await SendMessagePrivateAsync(messageText);
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
    private void HoverStart(int? messageId, int? messageIndex = null)
    {
        if (messageId.HasValue)
        {
            _hoveredMessageId = messageId;
            _hoveredMessageIndex = null; // Clear index when using ID
        }
        else if (messageIndex.HasValue)
        {
            _hoveredMessageIndex = messageIndex;
            _hoveredMessageId = null; // Clear ID when using index
        }

        StateHasChanged();
    }

    /// <summary>
    /// Handles hover stop for a message bubble.
    /// </summary>
    private void HoverStop(int? messageId, int? messageIndex = null)
    {
        if ((messageId.HasValue && _hoveredMessageId == messageId) ||
            (messageIndex.HasValue && _hoveredMessageIndex == messageIndex))
        {
            _hoveredMessageId = null;
            _hoveredMessageIndex = null;
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
            MessageFeedbackResponse existing = _messageFeedback[messageId];
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
            {nameof(FeedbackDialog.MessageId), messageId},
            {nameof(FeedbackDialog.PreSelectedFeedback), isPositive},
            {
                nameof(FeedbackDialog.OnFeedbackSubmitted), EventCallback.Factory.Create<FeedbackSubmission?>(this,
                    async (FeedbackSubmission? feedback) =>
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
                    })
            }
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
                    _messageFeedback[messageId] = feedback;
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
    /// Shows the evaluation metrics page for a message.
    /// </summary>
    private void ShowMetricsDialogAsync(int messageId)
    {
        // Navigate to the dedicated metrics page
        NavigationManager.NavigateTo($"/admin/metrics/{messageId}");
    }

    /// <summary>
    /// Loads metadata (feedback, metrics, tool calls, sentiment) for the specified message IDs in a single batch request.
    /// </summary>
    private async Task LoadMessagesMetadataAsync(List<int> messageIds)
    {
        if (messageIds.Count == 0) return;

        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");

            var request = new MessagesMetadataRequest {MessageIds = messageIds};
            var response = await httpClient.PostAsJsonAsync("/messages/metadata", request);

            if (response.IsSuccessStatusCode)
            {
                var metadata = await response.Content.ReadFromJsonAsync<MessagesMetadataResponse>();
                if (metadata == null) return;

                foreach (var (msgId, fb) in metadata.Feedback)
                {
                    _messageFeedback[msgId] = fb;
                }

                // 2. Process Tool Calls
                foreach (var (msgId, items) in metadata.ToolCalls)
                {
                    _messageToolCalls[msgId] = items;
                }

                // 3. Process Metrics
                foreach (var (msgId, items) in metadata.Metrics)
                {
                    _messageMetrics[msgId] = items;
                }

                // 4. Process Sentiment
                foreach (var (msgId, sent) in metadata.Sentiment)
                {
                    _messageSentiment[msgId] = new MessageSentimentInfo
                    {
                        SentimentId = sent.SentimentId,
                        Sentiment = sent.Sentiment,
                        Confidence = sent.Confidence,
                        SentimentSource = sent.SentimentSource
                    };
                }

                _logger?.LogInformation("Loaded metadata for {Count} messages", messageIds.Count);
            }
            else
            {
                _logger?.LogError("Failed to load message metadata. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load message metadata");
        }
    }

    /// <summary>
    /// Connects to the SignalR hub for real-time message updates.
    /// </summary>
    private async Task ConnectToSignalRHubAsync()
    {
        try
        {
            // Disconnect from previous hub if game changed
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            // Build hub connection using the API service URL
            HttpClient apiClient = HttpClientFactory.CreateClient("Api");
            string hubUrl = new Uri(apiClient.BaseAddress!, "/hubs/messages").ToString();

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Handle message updates
            _hubConnection.On<MessageUpdateNotification>("MessageUpdated",
                async notification =>
                {
                    _logger?.LogDebug(
                        "Received {UpdateType} update for message {MessageId}",
                        notification.UpdateType,
                        notification.MessageId);

                    await InvokeAsync(async () =>
                    {
                        // Update local state based on update type
                        if (notification.UpdateType == MessageUpdateType.SentimentAnalyzed &&
                            notification.Sentiment.HasValue)
                        {
                            // Store sentiment info
                            _messageSentiment[notification.MessageId] = new MessageSentimentInfo
                            {
                                Sentiment = notification.Sentiment.Value,
                                Confidence = notification.SentimentConfidence,
                                SentimentSource = notification.SentimentSource
                            };

                            // Match by content using text from notification
                            // Only proceed if we don't already have this message ID mapped
                            if (!_messageIds.Contains(notification.MessageId) &&
                                !string.IsNullOrWhiteSpace(notification.MessageText))
                            {
                                string normalizedText = notification.MessageText.Trim();

                                for (int i = 0; i < _messages.Count; i++)
                                {
                                    if (_messageIds[i] == null &&
                                        _messages[i].Role == ChatRole.User &&
                                        _messages[i].Text.Trim().Equals(normalizedText, StringComparison.Ordinal))
                                    {
                                        _messageIds[i] = notification.MessageId;
                                        _logger?.LogDebug(
                                            "Assigned User message ID {MessageId} to index {Index} by content matching",
                                            notification.MessageId,
                                            i);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (notification.UpdateType == MessageUpdateType.MetricsEvaluated &&
                                 notification.Metrics != null)
                        {
                            _messageMetrics[notification.MessageId] = notification.Metrics;

                            // Match by content using text from notification
                            // Only proceed if we don't already have this message ID mapped
                            if (!_messageIds.Contains(notification.MessageId) &&
                                !string.IsNullOrWhiteSpace(notification.MessageText))
                            {
                                string normalizedText = notification.MessageText.Trim();

                                for (int i = 0; i < _messages.Count; i++)
                                {
                                    if (_messageIds[i] == null &&
                                        _messages[i].Role == ChatRole.Assistant &&
                                        _messages[i].Text.Trim().Equals(normalizedText, StringComparison.Ordinal))
                                    {
                                        _messageIds[i] = notification.MessageId;
                                        _logger?.LogDebug(
                                            "Assigned Assistant message ID {MessageId} to index {Index} by content matching",
                                            notification.MessageId,
                                            i);
                                        break;
                                    }
                                }
                            }
                        }

                        StateHasChanged();
                    });
                });

            // Start connection
            await _hubConnection.StartAsync();
            _logger?.LogDebug("Connected to SignalR hub");

            // Join the game group to receive updates for this game
            await _hubConnection.InvokeAsync("JoinGame", GameId);
            _logger?.LogDebug("Joined game group {GameId}", GameId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to connect to SignalR hub. Real-time updates will not be available.");
            // Don't throw - the page should still work, just without real-time updates
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.InvokeAsync("LeaveGame", GameId);
            }
            catch
            {
                // Ignore errors when leaving game group during disposal
            }

            await _hubConnection.DisposeAsync();
        }
    }
}

/// <summary>
/// Helper class to store sentiment information including confidence.
/// </summary>
public class MessageSentimentInfo
{
    public int? SentimentId { get; set; }
    public int Sentiment { get; set; }
    public double? Confidence { get; set; }
    public int? SentimentSource { get; set; }
}

/// <summary>
/// Helper class to store agent info for messages.
/// </summary>
public class MessageAgentInfo
{
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }
    public int? InstructionVersionId { get; set; }
    public string? VersionNumber { get; set; }
    public bool IsScriptedMessage { get; set; }
}
