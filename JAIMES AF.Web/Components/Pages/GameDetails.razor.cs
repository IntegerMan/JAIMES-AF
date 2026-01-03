using System.Net.Http.Json;
using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
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
    private HashSet<int> _streamingMessageIndexes = new(); // Track which message indexes are currently streaming
    private Dictionary<int, MessageFeedbackResponse> _messageFeedback = new();
    private Dictionary<int, List<MessageToolCallResponse>> _messageToolCalls = new();
    private Dictionary<int, List<MessageEvaluationMetricResponse>> _messageMetrics = new();
    private Dictionary<int, MessageSentimentInfo> _messageSentiment = new();
    private Dictionary<int, bool> _messageHasMissingEvaluators = new();
    private Dictionary<int, MessageAgentInfo> _messageAgentInfo = new();
    private string? _defaultAgentId;
    private string? _defaultAgentName;
    private int? _defaultInstructionVersionId;
    private string? _defaultVersionNumber;

    private GameStateResponse? _game;
    private bool _isLoading = true;
    private string? _errorMessage;
    private int? _failedMessageIndex;
    private HubConnection? _hubConnection;

    // Title editing
    private string? _editableTitle;
    private bool _isSavingTitle = false;
    private bool _isEditingTitle = false;

    // Agent and Version selection
    private List<AgentResponse> _availableAgents = [];
    private List<AgentInstructionVersionResponse> _availableVersions = [];
    private string? _selectedAgentId;
    private int? _selectedVersionId;
    private bool _isChangingAgent = false;


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
    private Guid _chatInputKey = Guid.NewGuid();
    private MudTextField<string>? _chatInput;

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
                    var info = new MessageAgentInfo
                    {
                        AgentId = message.AgentId,
                        AgentName = message.AgentName,
                        InstructionVersionId = message.InstructionVersionId,
                        VersionNumber = message.VersionNumber,
                        IsScriptedMessage = message.IsScriptedMessage
                    };
                    _messageAgentInfo[message.Id] = info;

                    // Capture default agent info from history (e.g. from the first or any message)
                    if (_defaultAgentId == null && !string.IsNullOrEmpty(info.AgentId))
                    {
                        _defaultAgentId = info.AgentId;
                        _defaultAgentName = info.AgentName;
                        _defaultInstructionVersionId = info.InstructionVersionId;
                        _defaultVersionNumber = info.VersionNumber;
                    }
                }
            }

            // Initialize editable title
            _editableTitle = _game?.Title;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load game from API");
            _errorMessage = "Failed to load game: " + ex.Message;
        }
        finally
        {
            if (_game != null)
            {
                _selectedAgentId = _game.AgentId ?? _defaultAgentId;
                _selectedVersionId = _game.InstructionVersionId;
                _defaultAgentName ??= _game.AgentName;
                _defaultVersionNumber ??= _game.VersionNumber;

                if (!string.IsNullOrEmpty(_selectedAgentId))
                {
                    await LoadAvailableVersionsAsync(_selectedAgentId);
                }
            }

            await LoadAvailableAgentsAsync();

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

        // Clear input immediately so user sees it's been sent
        _userMessage.Text = string.Empty;
        _chatInputKey = Guid.NewGuid();

        // Send the message (this will add user message and trigger scroll)
        await SendMessagePrivateAsync(message);

        // Reset focus after sending
        await Task.Yield();

        if (_chatInput != null)
        {
            await _chatInput.FocusAsync();
        }
    }

    private async Task SendMessagePrivateAsync(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText) || _isSending) return;

        _isSending = true;
        _errorMessage = null;
        _failedMessageIndex = null;
        int currentMessageIndex = -1;

        try
        {
            // Add user message to UI
            _messages.Add(new(ChatRole.User, messageText));
            _messageIds.Add(null); // Will be set when we receive the persisted event
            currentMessageIndex = _messages.Count - 1;

            _logger?.LogInformation("Sending message {Text} from User", messageText);

            // Scroll to bottom immediately after adding user message and showing typing indicator
            await InvokeAsync(async () =>
            {
                StateHasChanged();
                await ScrollToBottomAsync();
            });

            // Send message to API using SSE
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            ChatRequest request = new()
            {
                GameId = GameId,
                Message = messageText
            };

            HttpRequestMessage httpRequest = new(HttpMethod.Post, $"/games/{GameId}/chat")
            {
                Content = JsonContent.Create(request)
            };

            HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Track accumulated text per message ID
            Dictionary<string, System.Text.StringBuilder> messageBuilders = new();
            Dictionary<string, int> messageIndexes = new();

            // Read SSE stream
            await using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new(stream);

            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Parse SSE format: "event: eventType" and "data: jsonData"
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    string eventType = line.Substring(7);

                    // Read the data line
                    string? dataLine = await reader.ReadLineAsync();
                    if (dataLine == null || !dataLine.StartsWith("data: ", StringComparison.Ordinal)) continue;

                    string jsonData = dataLine.Substring(6);

                    if (eventType == "delta")
                    {
                        // Parse streaming delta
                        var delta = JsonSerializer.Deserialize<ChatStreamEvent>(jsonData, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (delta == null) continue;

                        // Get or create message builder
                        if (!messageBuilders.TryGetValue(delta.MessageId, out var sb))
                        {
                            sb = new System.Text.StringBuilder();
                            messageBuilders[delta.MessageId] = sb;
                        }

                        // Append delta
                        if (!string.IsNullOrEmpty(delta.TextDelta))
                        {
                            sb.Append(delta.TextDelta);
                        }

                        string accumulatedText = sb.ToString();

                        // Check if message already exists in UI
                        bool isExisting = messageIndexes.TryGetValue(delta.MessageId, out int msgIndex);

                        // Only show message if we have content
                        if (string.IsNullOrWhiteSpace(accumulatedText) && !isExisting)
                        {
                            continue;
                        }

                        // Stop typing indicator once we have content
                        if (_isSending)
                        {
                            _isSending = false;
                            await InvokeAsync(StateHasChanged);
                        }

                        if (!isExisting)
                        {
                            // Create new message
                            var newMessage = new ChatMessage(ChatRole.Assistant, accumulatedText)
                            {
                                AuthorName = delta.AuthorName
                            };

                            _messages.Add(newMessage);
                            _messageIds.Add(null); // Will be set in persisted event
                            msgIndex = _messages.Count - 1;
                            messageIndexes[delta.MessageId] = msgIndex;

                            // Mark this message as streaming
                            _streamingMessageIndexes.Add(msgIndex);
                        }
                        else
                        {
                            // Update existing message
                            var oldMsg = _messages[msgIndex];
                            _messages[msgIndex] = new ChatMessage(oldMsg.Role, accumulatedText)
                            {
                                AuthorName = delta.AuthorName ?? oldMsg.AuthorName
                            };
                        }

                        // Update UI and scroll immediately during streaming
                        await InvokeAsync(async () =>
                        {
                            StateHasChanged();
                            // Scroll immediately without waiting for AfterRender
                            await ScrollToBottomAsync();
                        });
                    }
                    else if (eventType == "persisted")
                    {
                        // Parse persisted message IDs
                        var persistedData = JsonSerializer.Deserialize<MessagePersistedEvent>(jsonData, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (persistedData != null)
                        {
                            _logger?.LogInformation("Received persisted IDs - User: {UserId}, Assistant: {AssistantIds}",
                                persistedData.UserMessageId,
                                string.Join(",", persistedData.AssistantMessageIds ?? new List<int>()));

                            // Set user message ID
                            if (persistedData.UserMessageId.HasValue && currentMessageIndex >= 0)
                            {
                                _messageIds[currentMessageIndex] = persistedData.UserMessageId.Value;

                                // Add agent info for this user message
                                _messageAgentInfo[persistedData.UserMessageId.Value] = new MessageAgentInfo
                                {
                                    AgentId = _defaultAgentId,
                                    AgentName = _defaultAgentName,
                                    InstructionVersionId = _defaultInstructionVersionId,
                                    VersionNumber = _defaultVersionNumber,
                                    IsScriptedMessage = false
                                };
                            }

                            // Set assistant message IDs
                            if (persistedData.AssistantMessageIds != null)
                            {
                                int assistantStartIndex = currentMessageIndex + 1;
                                for (int i = 0; i < persistedData.AssistantMessageIds.Count; i++)
                                {
                                    int messageIndex = assistantStartIndex + i;
                                    if (messageIndex < _messageIds.Count)
                                    {
                                        int dbId = persistedData.AssistantMessageIds[i];
                                        _messageIds[messageIndex] = dbId;

                                        // Add agent info for assistant message
                                        _messageAgentInfo[dbId] = new MessageAgentInfo
                                        {
                                            AgentId = _defaultAgentId,
                                            AgentName = _defaultAgentName,
                                            InstructionVersionId = _defaultInstructionVersionId,
                                            VersionNumber = _defaultVersionNumber,
                                            IsScriptedMessage = false
                                        };
                                    }
                                }
                            }

                            await InvokeAsync(StateHasChanged);
                        }
                    }
                    else if (eventType == "done")
                    {
                        _logger?.LogInformation("Stream completed successfully");

                        // Clear streaming status for all messages that were streaming
                        _streamingMessageIndexes.Clear();
                        await InvokeAsync(StateHasChanged);

                        break;
                    }
                    else if (eventType == "error")
                    {
                        var errorData = JsonSerializer.Deserialize<ErrorEvent>(jsonData, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        _errorMessage = $"Server error: {errorData?.Error ?? "Unknown error"}";
                        _failedMessageIndex = currentMessageIndex;
                        break;
                    }
                }
            }

            _logger?.LogInformation("Finished streaming loop");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send chat message");
            _errorMessage = $"Failed to send message: {ex.Message}";
            _failedMessageIndex = currentMessageIndex != -1 ? currentMessageIndex : _messages.Count - 1;
        }
        finally
        {
            _logger?.LogInformation("Entering Finally block. IsSending: {IsSending}", _isSending);
            _isSending = false;

            // Scroll after typing indicator disappears
            _shouldScrollToBottom = true;
            await InvokeAsync(StateHasChanged);
        }
    }

    // SSE event models
    private record ChatStreamEvent(string MessageId, string TextDelta, string Role, string? AuthorName);
    private record MessagePersistedEvent(string Type, int? UserMessageId, List<int>? AssistantMessageIds);
    private record ErrorEvent(string Error, string Type);

    private string updatedTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length > 20 ? text.Substring(0, 20) : text;
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

    private HashSet<int> _loadedMetricsMessageIds = new();

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

                // Track that we have loaded metadata for these messages
                _loadedMetricsMessageIds.UnionWith(messageIds);

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

                foreach (var (msgId, hasMissing) in metadata.HasMissingEvaluators)
                {
                    _messageHasMissingEvaluators[msgId] = hasMissing;
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
                        // Note: We no longer need content matching since IDs are received immediately via SSE
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
                        }
                        else if (notification.UpdateType == MessageUpdateType.MetricsEvaluated &&
                                 notification.Metrics != null)
                        {
                            _messageMetrics[notification.MessageId] = notification.Metrics;
                            _loadedMetricsMessageIds.Add(notification.MessageId);

                            if (notification.HasMissingEvaluators.HasValue)
                            {
                                _messageHasMissingEvaluators[notification.MessageId] =
                                    notification.HasMissingEvaluators.Value;
                            }
                        }
                        else if (notification.UpdateType == MessageUpdateType.ToolCallsProcessed &&
                                 notification.HasToolCalls == true)
                        {
                            // Fetch metadata for this message immediately to get the tool calls
                            await LoadMessagesMetadataAsync(new List<int> {notification.MessageId});
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
            _logger?.LogError(ex, "Failed to connect to SignalR hub");
        }
    }

    private async Task LoadAvailableAgentsAsync()
    {
        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            var response = await httpClient.GetFromJsonAsync<AgentListResponse>("/agents");
            if (response?.Agents != null)
            {
                // Filter to just game master role agents as requested
                var agents = response.Agents
                    .Where(a => a.Role.Equals("Game Master", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Ensure the currently selected agent is in the list, even if it's not a "Game Master"
                if (!string.IsNullOrEmpty(_selectedAgentId) && !agents.Any(a => a.Id == _selectedAgentId))
                {
                    var selectedAgent = response.Agents.FirstOrDefault(a => a.Id == _selectedAgentId);
                    if (selectedAgent != null)
                    {
                        agents.Add(selectedAgent);
                    }
                }

                _availableAgents = agents.OrderBy(a => a.Name).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load available agents");
        }
    }

    private async Task LoadAvailableVersionsAsync(string agentId)
    {
        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            var response =
                await httpClient.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                    $"/agents/{agentId}/instruction-versions");
            if (response?.InstructionVersions != null)
            {
                _availableVersions = response.InstructionVersions.OrderByDescending(v => v.VersionNumber).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load versions for agent {AgentId}", agentId);
        }
    }

    private async Task OnAgentChanged(string agentId)
    {
        if (_selectedAgentId == agentId) return;

        _selectedAgentId = agentId;
        _selectedVersionId = null;
        _availableVersions.Clear();

        if (!string.IsNullOrEmpty(agentId))
        {
            await LoadAvailableVersionsAsync(agentId);
            // Default to Dynamic/Latest (null) when agent changes
            await SaveAgentSelectionAsync();
        }

        StateHasChanged();
    }

    private async Task OnVersionChanged(int? versionId)
    {
        if (_selectedVersionId == versionId || _isChangingAgent) return;

        _selectedVersionId = versionId;
        if (!string.IsNullOrEmpty(_selectedAgentId))
        {
            await SaveAgentSelectionAsync();
        }

        StateHasChanged();
    }

    private async Task SaveAgentSelectionAsync()
    {
        if (_game == null || string.IsNullOrEmpty(_selectedAgentId)) return;

        _isChangingAgent = true;
        try
        {
            HttpClient httpClient = HttpClientFactory.CreateClient("Api");
            var request = new UpdateGameRequest
            {
                AgentId = _selectedAgentId,
                InstructionVersionId = _selectedVersionId
            };
            var response = await httpClient.PutAsJsonAsync($"/games/{GameId}", request);

            if (response.IsSuccessStatusCode)
            {
                var updatedGame = await response.Content.ReadFromJsonAsync<GameInfoResponse>();
                if (updatedGame != null)
                {
                    // Update local game state
                    // Note: GameStateResponse doesn't have AgentId/VersionId yet, but the API will return them if we update it
                    // For now, we trust the local selection and the API successfully saved it.
                    // The next message sent will use the new agent because of GameAwareAgentFactory changes.

                    // Re-initialize the agent to pick up new instructions
                    HttpClient aguiHttpClient = HttpClientFactory.CreateClient("AGUI");
                    string serverUrl = $"{aguiHttpClient.BaseAddress}games/{GameId}/chat";
                    AGUIChatClient chatClient = new(aguiHttpClient, serverUrl);
                    _agent = chatClient.CreateAIAgent(name: $"game-{GameId}", description: "Game Chat Agent");
                }
            }
            else
            {
                _logger?.LogError("Failed to save agent selection: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save agent selection");
        }
        finally
        {
            _isChangingAgent = false;
            StateHasChanged();
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
