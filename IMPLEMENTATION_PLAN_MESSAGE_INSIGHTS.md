# Implementation Plan: Message Insights Feature

## Overview
Add an "insights" button to the Agent Messages page that generates and displays coaching insights about what the AI game master is doing well and what needs improvement. This feature analyzes conversation fragments in batches and summarizes them to provide actionable coaching feedback for the Prompt Improvement Wizard.

## Architecture Pattern
Following the established pattern used by Feedback, Metrics, and Sentiment features:
- **UI Components**: Razor components with button and insights display
- **Backend API**: RESTful endpoint for insights generation
- **Service Layer**: `PromptImproverService` extension for message analysis
- **Batch Processing**: Analyze message fragments in chunks, then summarize
- **Integration**: Wire insights into the Prompt Improvement Wizard

---

## Implementation Steps

### 1. Database Schema (Optional - No Changes Required)
**Status**: ✅ No changes needed

The `Message` table already contains all necessary data:
- `Id`, `Text`, `GameId`, `AgentId`, `InstructionVersionId`
- Related data: `MessageFeedback`, `MessageEvaluationMetric`, `MessageToolCall`

We can analyze messages directly without new tables.

---

### 2. Backend Service Layer

#### 2.1 Add Message Insights System Prompt
**File**: `JAIMES AF.ApiService\Services\PromptImproverSystemPrompts.cs`

Add a new constant for message analysis coaching:

```csharp
/// <summary>
/// System prompt for analyzing conversation fragments and generating coaching insights.
/// </summary>
public const string MessageInsightsPrompt = """
    You are an AI coaching assistant helping improve a game master AI agent's performance.

    Analyze the conversation fragments provided from actual gameplay sessions.
    Identify patterns in the agent's behavior, tone, style, and effectiveness as a game master.
    Generate a concise paragraph of coaching recommendations.

    Focus on:
    - What the agent is doing well (narrative quality, player engagement, rule application)
    - What needs improvement (pacing, clarity, consistency, helpfulness)
    - Specific actionable recommendations for better gameplay experiences

    Keep your response under 200 words and be specific and constructive.
    """;

/// <summary>
/// System prompt for summarizing batch message insights into final coaching feedback.
/// </summary>
public const string MessageInsightsSummaryPrompt = """
    You are an AI coaching assistant synthesizing insights from multiple conversation analysis batches.

    You will be provided with insights from several batches of conversation fragments.
    Your task is to synthesize these into a single, coherent coaching message.

    Focus on:
    - Common themes across all batches
    - The most important patterns to address
    - Prioritized, actionable recommendations
    - Consolidating similar insights to avoid redundancy

    Keep your response under 250 words and be specific and constructive.
    This coaching message will be used to improve the agent's system prompt.
    """;
```

#### 2.2 Extend PromptImproverService
**File**: `JAIMES AF.ApiService\Services\PromptImproverService.cs`

Add methods for message insights generation:

```csharp
private const int MessageBatchSize = 20; // Messages per batch
private const int MaxMessageBatches = 5; // Maximum batches to analyze (100 messages total)

/// <summary>
/// Generates insights from conversation message fragments.
/// Analyzes messages in batches, then summarizes the batch insights.
/// </summary>
public async Task<GenerateInsightsResponse> GenerateMessageInsightsAsync(
    string agentId,
    int versionId,
    CancellationToken cancellationToken = default)
{
    try
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get messages for this agent version
        List<Message> messages = await context.Messages
            .Where(m => m.AgentId == agentId && m.InstructionVersionId == versionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MessageBatchSize * MaxMessageBatches)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "messages",
                ItemsAnalyzed = 0,
                Insights = "No message data available for analysis."
            };
        }

        // Get the current prompt for context
        string currentPrompt = await GetCurrentPromptAsync(context, versionId, cancellationToken);

        // Split messages into batches
        List<List<Message>> batches = SplitIntoBatches(messages, MessageBatchSize);

        // Analyze each batch in parallel
        List<Task<string>> batchTasks = batches
            .Select(batch => AnalyzeMessageBatchAsync(batch, currentPrompt, cancellationToken))
            .ToList();

        string[] batchInsights = await Task.WhenAll(batchTasks);

        // If only one batch, return its insights directly
        if (batchInsights.Length == 1)
        {
            return new GenerateInsightsResponse
            {
                Success = true,
                InsightType = "messages",
                ItemsAnalyzed = messages.Count,
                Insights = batchInsights[0].Trim()
            };
        }

        // Summarize all batch insights into final coaching message
        string finalInsights = await SummarizeBatchInsightsAsync(batchInsights, cancellationToken);

        string trimmedInsights = finalInsights.Trim();
        if (string.IsNullOrWhiteSpace(trimmedInsights))
        {
            return new GenerateInsightsResponse
            {
                Success = false,
                InsightType = "messages",
                Error = "The AI returned an empty response for message insights. Please try again."
            };
        }

        return new GenerateInsightsResponse
        {
            Success = true,
            InsightType = "messages",
            ItemsAnalyzed = messages.Count,
            Insights = trimmedInsights
        };
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to generate message insights for agent {AgentId} version {VersionId}",
            agentId, versionId);
        return new GenerateInsightsResponse
        {
            Success = false,
            InsightType = "messages",
            Error = ex.Message
        };
    }
}

/// <summary>
/// Analyzes a single batch of messages and returns coaching insights.
/// </summary>
private async Task<string> AnalyzeMessageBatchAsync(
    List<Message> messages,
    string currentPrompt,
    CancellationToken cancellationToken)
{
    string userMessage = BuildMessageInsightsPrompt(messages, currentPrompt);

    return await GetAiResponseAsync(
        PromptImproverSystemPrompts.MessageInsightsPrompt,
        userMessage,
        cancellationToken);
}

/// <summary>
/// Summarizes insights from multiple message batches into a final coaching message.
/// </summary>
private async Task<string> SummarizeBatchInsightsAsync(
    string[] batchInsights,
    CancellationToken cancellationToken)
{
    StringBuilder sb = new();
    sb.AppendLine("## Batch Insights to Synthesize");
    sb.AppendLine();

    for (int i = 0; i < batchInsights.Length; i++)
    {
        sb.AppendLine($"### Batch {i + 1}");
        sb.AppendLine(batchInsights[i]);
        sb.AppendLine();
    }

    return await GetAiResponseAsync(
        PromptImproverSystemPrompts.MessageInsightsSummaryPrompt,
        sb.ToString(),
        cancellationToken);
}

/// <summary>
/// Builds the prompt for message insights generation.
/// </summary>
public static string BuildMessageInsightsPrompt(List<Message> messages, string currentPrompt)
{
    StringBuilder sb = new();
    sb.AppendLine("## Current Agent Prompt");
    sb.AppendLine(currentPrompt);
    sb.AppendLine();
    sb.AppendLine("## Conversation Fragments");
    sb.AppendLine();

    foreach (Message message in messages)
    {
        sb.AppendLine("---");
        sb.AppendLine($"**Message {message.Id}** ({message.CreatedAt:yyyy-MM-dd HH:mm})");
        sb.AppendLine();
        sb.AppendLine(message.Text);
        sb.AppendLine();
    }

    return sb.ToString();
}

/// <summary>
/// Splits a list into batches of specified size.
/// </summary>
private static List<List<T>> SplitIntoBatches<T>(List<T> items, int batchSize)
{
    List<List<T>> batches = new();

    for (int i = 0; i < items.Count; i += batchSize)
    {
        batches.Add(items.Skip(i).Take(batchSize).ToList());
    }

    return batches;
}
```

#### 2.3 Update GenerateInsightsAsync Switch
**File**: `JAIMES AF.ApiService\Services\PromptImproverService.cs`

Update the switch statement in `GenerateInsightsAsync`:

```csharp
return insightType.ToLowerInvariant() switch
{
    "feedback" => await GenerateFeedbackInsightsAsync(agentId, versionId, cancellationToken),
    "metrics" => await GenerateMetricsInsightsAsync(agentId, versionId, cancellationToken),
    "sentiment" => await GenerateSentimentInsightsAsync(agentId, versionId, cancellationToken),
    "messages" => await GenerateMessageInsightsAsync(agentId, versionId, cancellationToken), // NEW
    _ => new GenerateInsightsResponse
    {
        Success = false,
        Error = $"Unknown insight type: {insightType}",
        InsightType = insightType
    }
};
```

---

### 3. API Endpoint

**Status**: ✅ No changes required

The existing `GenerateInsightsEndpoint.cs` already handles all insight types dynamically via the `InsightType` parameter. It will automatically support "messages" insights once the service method is added.

**Endpoint**: `POST /agents/{agentId}/instruction-versions/{versionId}/generate-insights`

**Request Body**:
```json
{
  "insightType": "messages"
}
```

---

### 4. Frontend UI Components

#### 4.1 Add Insights Button to AgentMessagesList
**File**: `JAIMES AF.Web\Components\Agents\AgentMessagesList.razor`

Add insights generation UI similar to `MetricsGrid.razor`:

```razor
@using MattEland.Jaimes.ServiceDefinitions.Responses
@using MattEland.Jaimes.ServiceDefinitions.Requests
@using MattEland.Jaimes.Web.Components.Chat
@inject HttpClient Http
@inject ILoggerFactory LoggerFactory
@inject IHttpClientFactory HttpClientFactory
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudDataGrid Items="_messages" Filterable="true" Groupable="true" Loading="_isLoading">
    <!-- Existing columns unchanged -->
</MudDataGrid>

@* Add insights section below the grid *@
@if (VersionId.HasValue && !string.IsNullOrEmpty(AgentId))
{
    <MudPaper Class="pa-4 mt-4" Elevation="1">
        <MudStack Row="true" AlignItems="AlignItems.Center" Justify="Justify.SpaceBetween">
            <MudText Typo="Typo.h6">Message Insights</MudText>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Secondary"
                       StartIcon="@Icons.Material.Filled.AutoAwesome"
                       OnClick="GenerateInsightsAsync"
                       Disabled="_isGeneratingInsights">
                @if (_isGeneratingInsights)
                {
                    <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true"/>
                    <span class="ms-2">Analyzing Messages...</span>
                }
                else
                {
                    <span>Generate Message Insights</span>
                }
            </MudButton>
        </MudStack>

        @if (!string.IsNullOrEmpty(_insightsResult))
        {
            <MudDivider Class="my-3"/>
            <MudText Typo="Typo.body1" Style="white-space: pre-wrap;">@_insightsResult</MudText>
            @if (_insightsItemCount > 0)
            {
                <MudText Typo="Typo.caption" Class="text-muted mt-2">
                    Based on @_insightsItemCount messages
                </MudText>
            }
        }
    </MudPaper>
}

@code {
    [Parameter] public string AgentId { get; set; } = string.Empty;
    [Parameter] public int? VersionId { get; set; }

    private List<MessageContextDto> _messages = new();
    private bool _isLoading = true;

    // Insights generation state
    private bool _isGeneratingInsights;
    private string? _insightsResult;
    private int _insightsItemCount;

    protected override async Task OnInitializedAsync()
    {
        await LoadMessagesAsync();
    }

    private async Task LoadMessagesAsync()
    {
        // Existing implementation unchanged
    }

    /// <summary>
    /// Generates AI insights from conversation messages and shows them in a dialog.
    /// </summary>
    public async Task GenerateInsightsAsync()
    {
        if (!VersionId.HasValue || string.IsNullOrEmpty(AgentId))
            return;

        _isGeneratingInsights = true;
        StateHasChanged();

        try
        {
            var client = HttpClientFactory.CreateClient("Api");
            var request = new GenerateInsightsRequest { InsightType = "messages" };

            var response = await client.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{VersionId}/generate-insights",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>();
                if (result?.Success == true)
                {
                    _insightsResult = result.Insights;
                    _insightsItemCount = result.ItemsAnalyzed;

                    // Show dialog with insights
                    var parameters = new DialogParameters<InsightsDialog>
                    {
                        { x => x.Title, "Message Insights" },
                        { x => x.Content, result.Insights ?? "No insights available." },
                        { x => x.ItemCount, result.ItemsAnalyzed }
                    };
                    await DialogService.ShowAsync<InsightsDialog>("AI Insights", parameters);
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate insights", Severity.Error);
                }
            }
            else
            {
                Snackbar.Add("Failed to generate insights", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingInsights = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Returns true if insights generation is in progress.
    /// </summary>
    public bool IsGeneratingInsights => _isGeneratingInsights;
}
```

---

### 5. Prompt Improvement Wizard Integration

#### 5.1 Update PromptImproverWizard.razor
**File**: `JAIMES AF.Web\Components\Pages\PromptImproverWizard.razor`

Add a fourth insights panel in Step 1:

```razor
@* Step 1: Gather Insights *@
@if (_activeStep == 1)
{
    <MudText Typo="Typo.h6" Class="mb-4">Step 1: Gather AI-Generated Insights</MudText>
    <MudText Typo="Typo.body2" Class="mb-4">
        We'll analyze data from feedback, metrics, sentiment, and conversation messages to generate coaching insights.
    </MudText>

    <MudGrid>
        <!-- Existing Feedback Panel -->
        <MudItem xs="12" md="6">
            <InsightGenerationPanel
                Title="Feedback Insights"
                InsightType="feedback"
                @bind-Insights="_feedbackInsights"
                @bind-IsGenerating="_isGeneratingFeedbackInsights"
                AgentId="@AgentId"
                VersionId="@_effectiveVersionId"
                OnGenerate="@(() => GenerateInsightsAsync("feedback"))" />
        </MudItem>

        <!-- Existing Metrics Panel -->
        <MudItem xs="12" md="6">
            <InsightGenerationPanel
                Title="Metrics Insights"
                InsightType="metrics"
                @bind-Insights="_metricsInsights"
                @bind-IsGenerating="_isGeneratingMetricsInsights"
                AgentId="@AgentId"
                VersionId="@_effectiveVersionId"
                OnGenerate="@(() => GenerateInsightsAsync("metrics"))" />
        </MudItem>

        <!-- Existing Sentiment Panel -->
        <MudItem xs="12" md="6">
            <InsightGenerationPanel
                Title="Sentiment Insights"
                InsightType="sentiment"
                @bind-Insights="_sentimentInsights"
                @bind-IsGenerating="_isGeneratingSentimentInsights"
                AgentId="@AgentId"
                VersionId="@_effectiveVersionId"
                OnGenerate="@(() => GenerateInsightsAsync("sentiment"))" />
        </MudItem>

        <!-- NEW: Message Insights Panel -->
        <MudItem xs="12" md="6">
            <InsightGenerationPanel
                Title="Message Insights"
                InsightType="messages"
                @bind-Insights="_messageInsights"
                @bind-IsGenerating="_isGeneratingMessageInsights"
                AgentId="@AgentId"
                VersionId="@_effectiveVersionId"
                OnGenerate="@(() => GenerateInsightsAsync("messages"))" />
        </MudItem>
    </MudGrid>

    <MudStack Row="true" Justify="Justify.End" Class="mt-4">
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="NextStep">
            Next Step
        </MudButton>
    </MudStack>
}
```

#### 5.2 Update PromptImproverWizard.razor.cs
**File**: `JAIMES AF.Web\Components\Pages\PromptImproverWizard.razor.cs`

Add state for message insights:

```csharp
// Existing fields
private string? _feedbackInsights;
private string? _metricsInsights;
private string? _sentimentInsights;
private string? _messageInsights; // NEW

private bool _isGeneratingFeedbackInsights;
private bool _isGeneratingMetricsInsights;
private bool _isGeneratingSentimentInsights;
private bool _isGeneratingMessageInsights; // NEW

protected override async Task OnInitializedAsync()
{
    // Load agent and version...

    // Fire and forget: Generate all four insights in parallel
    _ = GenerateInsightsAsync("feedback");
    _ = GenerateInsightsAsync("metrics");
    _ = GenerateInsightsAsync("sentiment");
    _ = GenerateInsightsAsync("messages"); // NEW
}

private async Task GenerateInsightsAsync(string insightType)
{
    // Existing implementation handles all types dynamically
    // Just ensure the switch/if statement includes "messages"

    switch (insightType)
    {
        case "feedback":
            _isGeneratingFeedbackInsights = true;
            break;
        case "metrics":
            _isGeneratingMetricsInsights = true;
            break;
        case "sentiment":
            _isGeneratingSentimentInsights = true;
            break;
        case "messages": // NEW
            _isGeneratingMessageInsights = true;
            break;
    }

    StateHasChanged();

    try
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
            new GenerateInsightsRequest { InsightType = insightType },
            _cancellationTokenSource.Token);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>();

            switch (insightType)
            {
                case "feedback":
                    _feedbackInsights = result?.Insights;
                    break;
                case "metrics":
                    _metricsInsights = result?.Insights;
                    break;
                case "sentiment":
                    _sentimentInsights = result?.Insights;
                    break;
                case "messages": // NEW
                    _messageInsights = result?.Insights;
                    break;
            }
        }
    }
    finally
    {
        switch (insightType)
        {
            case "feedback":
                _isGeneratingFeedbackInsights = false;
                break;
            case "metrics":
                _isGeneratingMetricsInsights = false;
                break;
            case "sentiment":
                _isGeneratingSentimentInsights = false;
                break;
            case "messages": // NEW
                _isGeneratingMessageInsights = false;
                break;
        }

        StateHasChanged();
    }
}
```

#### 5.3 Update GenerateImprovedPromptRequest
**File**: `JAIMES AF.ServiceDefinitions\Requests\GenerateImprovedPromptRequest.cs`

Add `MessageInsights` property:

```csharp
public class GenerateImprovedPromptRequest
{
    public string CurrentPrompt { get; set; } = string.Empty;
    public string? UserFeedback { get; set; }
    public string? FeedbackInsights { get; set; }
    public string? MetricsInsights { get; set; }
    public string? SentimentInsights { get; set; }
    public string? MessageInsights { get; set; } // NEW
}
```

#### 5.4 Update BuildImprovedPromptInput Method
**File**: `JAIMES AF.ApiService\Services\PromptImproverService.cs`

Include message insights in the prompt:

```csharp
public static string BuildImprovedPromptInput(GenerateImprovedPromptRequest request)
{
    StringBuilder sb = new();
    sb.AppendLine("## Current Prompt to Improve");
    sb.AppendLine(request.CurrentPrompt);
    sb.AppendLine();

    if (!string.IsNullOrWhiteSpace(request.UserFeedback))
    {
        sb.AppendLine("## User's Specific Requests");
        sb.AppendLine(request.UserFeedback);
        sb.AppendLine();
    }

    if (!string.IsNullOrWhiteSpace(request.FeedbackInsights))
    {
        sb.AppendLine("## Insights from User Feedback");
        sb.AppendLine(request.FeedbackInsights);
        sb.AppendLine();
    }

    if (!string.IsNullOrWhiteSpace(request.MetricsInsights))
    {
        sb.AppendLine("## Insights from Evaluation Metrics");
        sb.AppendLine(request.MetricsInsights);
        sb.AppendLine();
    }

    if (!string.IsNullOrWhiteSpace(request.SentimentInsights))
    {
        sb.AppendLine("## Insights from Sentiment Analysis");
        sb.AppendLine(request.SentimentInsights);
        sb.AppendLine();
    }

    // NEW
    if (!string.IsNullOrWhiteSpace(request.MessageInsights))
    {
        sb.AppendLine("## Insights from Conversation Messages");
        sb.AppendLine(request.MessageInsights);
        sb.AppendLine();
    }

    sb.AppendLine("Generate an improved version of the prompt that addresses the insights above.");

    return sb.ToString();
}
```

#### 5.5 Update Wizard Step 3 to Include Message Insights
**File**: `JAIMES AF.Web\Components\Pages\PromptImproverWizard.razor.cs`

Update the `GenerateImprovedPromptAsync` method to include message insights:

```csharp
private async Task GenerateImprovedPromptAsync()
{
    var request = new GenerateImprovedPromptRequest
    {
        CurrentPrompt = _currentPrompt ?? "",
        UserFeedback = _userFeedback,
        FeedbackInsights = _feedbackInsights,
        MetricsInsights = _metricsInsights,
        SentimentInsights = _sentimentInsights,
        MessageInsights = _messageInsights // NEW
    };

    // Rest of implementation unchanged...
}
```

---

## Testing Plan

### Unit Tests
1. **PromptImproverService.BuildMessageInsightsPrompt**
   - Test with empty message list
   - Test with single message
   - Test with multiple messages
   - Verify format includes current prompt and conversation fragments

2. **PromptImproverService.GenerateMessageInsightsAsync**
   - Test with no messages (returns "No message data available")
   - Test with messages under batch size (single batch)
   - Test with messages over batch size (multiple batches + summarization)
   - Test error handling

### Integration Tests
1. **API Endpoint**
   - POST `/agents/{agentId}/instruction-versions/{versionId}/generate-insights` with `insightType: "messages"`
   - Verify response contains `Success: true`, `ItemsAnalyzed`, and `Insights`

2. **Wizard Integration**
   - Verify all four insight panels load
   - Verify message insights populate correctly
   - Verify improved prompt includes message insights section

### Manual Testing
1. Navigate to Agent Messages page for an agent version with messages
2. Click "Generate Message Insights" button
3. Verify loading indicator appears
4. Verify insights dialog displays with message count
5. Navigate to Prompt Improvement Wizard
6. Verify Message Insights panel generates automatically
7. Verify improved prompt incorporates message insights

---

## File Summary

### Files to Create
- **NONE** (All features fit into existing file structure)

### Files to Modify

#### Backend
1. `JAIMES AF.ApiService\Services\PromptImproverSystemPrompts.cs`
   - Add `MessageInsightsPrompt` constant
   - Add `MessageInsightsSummaryPrompt` constant

2. `JAIMES AF.ApiService\Services\PromptImproverService.cs`
   - Add `GenerateMessageInsightsAsync` method
   - Add `AnalyzeMessageBatchAsync` method
   - Add `SummarizeBatchInsightsAsync` method
   - Add `BuildMessageInsightsPrompt` method
   - Add `SplitIntoBatches<T>` helper method
   - Update `GenerateInsightsAsync` switch to include "messages"
   - Update `BuildImprovedPromptInput` to include message insights

3. `JAIMES AF.ServiceDefinitions\Requests\GenerateImprovedPromptRequest.cs`
   - Add `MessageInsights` property

#### Frontend
4. `JAIMES AF.Web\Components\Agents\AgentMessagesList.razor`
   - Add insights generation button and display section
   - Add injected dependencies: `IHttpClientFactory`, `IDialogService`, `ISnackbar`
   - Add `GenerateInsightsAsync` method
   - Add insights state fields

5. `JAIMES AF.Web\Components\Pages\PromptImproverWizard.razor`
   - Add fourth `InsightGenerationPanel` for messages

6. `JAIMES AF.Web\Components\Pages\PromptImproverWizard.razor.cs`
   - Add `_messageInsights` field
   - Add `_isGeneratingMessageInsights` field
   - Update `OnInitializedAsync` to generate message insights
   - Update `GenerateInsightsAsync` switch to handle "messages"
   - Update `GenerateImprovedPromptAsync` to include message insights in request

---

## Dependencies

### NuGet Packages
- **No new dependencies required**
- Uses existing: `Microsoft.Extensions.AI`, `Microsoft.EntityFrameworkCore`, `MudBlazor`

### External Services
- Uses existing `IChatClient` for AI generation
- Uses existing `JaimesDbContext` for data access

---

## Performance Considerations

1. **Batch Processing**: Limits message analysis to 100 messages max (5 batches × 20 messages)
2. **Parallel Processing**: Analyzes batches in parallel using `Task.WhenAll`
3. **Database Query**: Single query with `Take()` limit to avoid loading excessive data
4. **AI Token Usage**: Two-phase approach (batch analysis + summarization) manages token limits

---

## Security Considerations

1. **Authorization**: Existing endpoint security applies (no changes needed)
2. **Data Access**: Filters messages by `AgentId` and `InstructionVersionId` to ensure isolation
3. **Input Validation**: No user input in this feature (only system-generated insights)

---

## Future Enhancements

1. **Configurable Batch Sizes**: Add settings for `MessageBatchSize` and `MaxMessageBatches`
2. **Caching**: Cache insights for a version to avoid regenerating unnecessarily
3. **Filtering**: Allow filtering messages by date range, game, or player
4. **Export**: Add ability to export insights as PDF or text file
5. **Message Context**: Include surrounding messages (user prompts) for better analysis
6. **Tool Call Analysis**: Highlight messages with tool calls for focused evaluation

---

## Rollout Strategy

### Phase 1: Backend Implementation
- Implement service layer methods
- Add system prompts
- Update DTOs
- Test with unit tests

### Phase 2: UI Implementation
- Add insights button to Agent Messages page
- Test standalone insights generation

### Phase 3: Wizard Integration
- Add fourth panel to wizard
- Update wizard to include message insights in improved prompt
- End-to-end testing

### Phase 4: Polish & Documentation
- Add tooltips and help text
- Update user documentation
- Performance tuning if needed

---

## Success Criteria

✅ Users can click "Generate Message Insights" on the Agent Messages page
✅ Insights are displayed in a dialog with message count
✅ Insights appear as a fourth panel in the Prompt Improvement Wizard
✅ Message insights are incorporated into the improved prompt generation
✅ Batch processing successfully handles large message volumes
✅ Performance is acceptable (< 30 seconds for 100 messages)
✅ Error handling provides clear feedback to users

---

## Implementation Estimate

- **Backend Service**: 2-3 hours
- **Frontend UI**: 1-2 hours
- **Wizard Integration**: 1 hour
- **Testing**: 2 hours
- **Documentation**: 30 minutes

**Total**: ~6-8 hours

---

## Notes

- This feature follows the exact same pattern as Feedback, Metrics, and Sentiment insights
- The batch processing approach is novel for this codebase but necessary due to message volume
- The two-phase AI analysis (batches + summary) keeps context manageable while providing comprehensive insights
- Integration into the wizard is straightforward since the infrastructure already exists
