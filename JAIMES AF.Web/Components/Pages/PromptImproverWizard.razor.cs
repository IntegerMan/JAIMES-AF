using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class PromptImproverWizard : IDisposable
{
    [Parameter] public string AgentId { get; set; } = string.Empty;
    [Parameter] public int? VersionId { get; set; }

    [Inject] public IJSRuntime JS { get; set; } = null!;

    private CancellationTokenSource? _cts;
    private bool _isLoading = true;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private int _activeStep;
    private int _maxReachedStep;

    // Step titles for the header
    private readonly string[] _stepTitles = ["Insights", "Feedback", "Generate", "Apply"];

    // Current prompt
    private string _currentPrompt = string.Empty;
    private int _effectiveVersionId;

    // Insights
    private string? _feedbackInsights;
    private string? _metricsInsights;
    private string? _sentimentInsights;
    private string? _messageInsights;
    private bool _isGeneratingFeedback;
    private bool _isGeneratingMetrics;
    private bool _isGeneratingSentiment;
    private bool _isGeneratingMessages;

    // User feedback
    private string _userFeedback = string.Empty;

    // Editable input prompt
    private string _editableInputPrompt = string.Empty;
    private bool _inputPromptExpanded = true;

    // Improved prompt
    private string _improvedPrompt = string.Empty;
    private bool _isGeneratingPrompt;

    private bool HasAnyInsights => !string.IsNullOrEmpty(_feedbackInsights) ||
                                   !string.IsNullOrEmpty(_metricsInsights) ||
                                   !string.IsNullOrEmpty(_sentimentInsights) ||
                                   !string.IsNullOrEmpty(_messageInsights);

    protected override async Task OnParametersSetAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new("Home", href: "/"),
            new("Admin", href: "/admin"),
            new("Agents", href: "/agents"),
            new("Agent", href: $"/agents/{AgentId}"),
            new("Improve Prompt", href: null, disabled: true)
        };

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (string.IsNullOrEmpty(AgentId))
        {
            _errorMessage = "Agent ID is required";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _errorMessage = null;
        ResetState();

        try
        {
            // Load agent details
            var agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{AgentId}");
            if (agent == null)
            {
                _errorMessage = "Agent not found";
                _isLoading = false;
                return;
            }

            // Update breadcrumbs with agent name
            _breadcrumbs = new List<BreadcrumbItem>
            {
                new("Home", href: "/"),
                new("Admin", href: "/admin"),
                new("Agents", href: "/agents"),
                new(agent.Name, href: $"/agents/{AgentId}"),
                new("Improve Prompt", href: null, disabled: true)
            };

            // Load the current prompt from the version or active version
            AgentInstructionVersionResponse? version = null;
            if (VersionId.HasValue)
            {
                version = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                    $"/agents/{AgentId}/instruction-versions/{VersionId}");
            }
            else
            {
                version = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                    $"/agents/{AgentId}/instruction-versions/active");
            }

            if (version == null)
            {
                _errorMessage = "Could not load agent instructions";
                _isLoading = false;
                return;
            }

            _currentPrompt = version.Instructions;
            _effectiveVersionId = version.Id;

            // Invalidate previous in-flight requests
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Auto-start insights generation with fire-and-forget pattern
            _ = Task.Run(async () =>
            {
                try
                {
                    await InvokeAsync(async () =>
                    {
                        var feedbackTask = GenerateFeedbackInsightsAsync(token);
                        var metricsTask = GenerateMetricsInsightsAsync(token);
                        var sentimentTask = GenerateSentimentInsightsAsync(token);
                        var messagesTask = GenerateMessageInsightsAsync(token);
                        await Task.WhenAll(feedbackTask, metricsTask, sentimentTask, messagesTask);
                    });
                }
                catch (ObjectDisposedException)
                {
                    /* Component was likely disposed */
                }
                catch (Exception ex)
                {
                    LoggerFactory.CreateLogger("PromptImproverWizard")
                        .LogError(ex, "Error in background insight generation");
                }
            }, token);
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("PromptImproverWizard")
                .LogError(ex, "Failed to load agent data");
            _errorMessage = "Failed to load agent data: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void ResetState()
    {
        _activeStep = 0;
        _maxReachedStep = 0;
        _feedbackInsights = null;
        _metricsInsights = null;
        _sentimentInsights = null;
        _messageInsights = null;
        _userFeedback = string.Empty;
        _improvedPrompt = string.Empty;
        _currentPrompt = string.Empty;
        _effectiveVersionId = 0;
        _isGeneratingFeedback = false;
        _isGeneratingMetrics = false;
        _isGeneratingSentiment = false;
        _isGeneratingMessages = false;
        _isGeneratingPrompt = false;
    }

    private async Task GenerateFeedbackInsightsAsync(CancellationToken cancellationToken = default)
    {
        _isGeneratingFeedback = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "feedback" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                if (result?.Success == true)
                {
                    _feedbackInsights = result.Insights;
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate feedback insights", Severity.Error);
                }
            }
            else
            {
                if (cancellationToken.IsCancellationRequested) return;
                Snackbar.Add("Failed to generate feedback insights", Severity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            /* Normal behavior on navigation */
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested) return;
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isGeneratingFeedback = false;
                StateHasChanged();
            }
        }
    }

    private async Task GenerateMetricsInsightsAsync(CancellationToken cancellationToken = default)
    {
        _isGeneratingMetrics = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "metrics" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                if (result?.Success == true)
                {
                    _metricsInsights = result.Insights;
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate metrics insights", Severity.Error);
                }
            }
            else
            {
                if (cancellationToken.IsCancellationRequested) return;
                Snackbar.Add("Failed to generate metrics insights", Severity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            /* Normal behavior on navigation */
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested) return;
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isGeneratingMetrics = false;
                StateHasChanged();
            }
        }
    }

    private async Task GenerateSentimentInsightsAsync(CancellationToken cancellationToken = default)
    {
        _isGeneratingSentiment = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "sentiment" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                if (result?.Success == true)
                {
                    _sentimentInsights = result.Insights;
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate sentiment insights", Severity.Error);
                }
            }
            else
            {
                if (cancellationToken.IsCancellationRequested) return;
                Snackbar.Add("Failed to generate sentiment insights", Severity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            /* Normal behavior on navigation */
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested) return;
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isGeneratingSentiment = false;
                StateHasChanged();
            }
        }
    }

    private async Task GenerateMessageInsightsAsync(CancellationToken cancellationToken = default)
    {
        _isGeneratingMessages = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "messages" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                if (result?.Success == true)
                {
                    _messageInsights = result.Insights;
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate message insights", Severity.Error);
                }
            }
            else
            {
                if (cancellationToken.IsCancellationRequested) return;
                Snackbar.Add("Failed to generate message insights", Severity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            /* Normal behavior on navigation */
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested) return;
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _isGeneratingMessages = false;
                StateHasChanged();
            }
        }
    }

    private async Task GenerateImprovedPromptAsync()
    {
        _isGeneratingPrompt = true;
        StateHasChanged();

        try
        {
            var request = new GenerateImprovedPromptRequest
            {
                CurrentPrompt = _currentPrompt,
                UserFeedback = string.IsNullOrWhiteSpace(_userFeedback) ? null : _userFeedback,
                FeedbackInsights = _feedbackInsights,
                MetricsInsights = _metricsInsights,
                SentimentInsights = _sentimentInsights,
                MessageInsights = _messageInsights,
                ManualInstructions = string.IsNullOrWhiteSpace(_editableInputPrompt) ? null : _editableInputPrompt
            };

            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-improved-prompt",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateImprovedPromptResponse>();
                if (result?.Success == true)
                {
                    _improvedPrompt = result.ImprovedPrompt ?? string.Empty;
                    Snackbar.Add("Improved prompt generated successfully!", Severity.Success);
                }
                else
                {
                    Snackbar.Add(result?.Error ?? "Failed to generate improved prompt", Severity.Error);
                }
            }
            else
            {
                Snackbar.Add("Failed to generate improved prompt", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingPrompt = false;
            StateHasChanged();
        }
    }

    private async Task CopyToClipboard()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", _improvedPrompt);
            Snackbar.Add("Copied to clipboard!", Severity.Info);
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("PromptImproverWizard")
                .LogError(ex, "Failed to copy to clipboard");
            Snackbar.Add("Failed to copy to clipboard. Ensure you are using HTTPS and have granted permission.",
                Severity.Error);
        }
    }

    private async Task ApplyImprovedPrompt()
    {
        // Navigate to edit page with the improved prompt stored in session
        // We need to store the prompt in session storage first
        try
        {
            await StoreAndNavigateAsync();
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("PromptImproverWizard")
                .LogError(ex, "Failed to store improved prompt in session storage");
            Snackbar.Add("Failed to apply improved prompt. Your browser storage may be full or disabled.",
                Severity.Error);
        }
    }

    private async Task StoreAndNavigateAsync()
    {
        // Store the improved prompt in session storage so the edit page can pick it up
        await JS.InvokeVoidAsync("sessionStorage.setItem", "improvedPrompt", _improvedPrompt);
        Navigation.NavigateTo($"/agents/{AgentId}/edit?baseVersionId={_effectiveVersionId}&improvedPrompt=true");
    }

    private void RegeneratePrompt()
    {
        _improvedPrompt = string.Empty;
        _activeStep = 2; // Go back to step 3 (Generate)
        StateHasChanged();
    }

    private void NextStep()
    {
        if (_activeStep < 3)
        {
            _activeStep++;
            if (_activeStep > _maxReachedStep)
            {
                _maxReachedStep = _activeStep;
            }

            // When advancing to step 2 (Generate), populate the editable input prompt
            if (_activeStep == 2)
            {
                _editableInputPrompt = GetFullPromptPreview();
            }

            StateHasChanged();
        }
    }

    private void PreviousStep()
    {
        if (_activeStep > 0)
        {
            _activeStep--;
            StateHasChanged();
        }
    }

    private async Task GenerateAndAdvanceAsync()
    {
        await GenerateImprovedPromptAsync();

        // If generation was successful, advance to step 4
        if (!string.IsNullOrEmpty(_improvedPrompt))
        {
            _activeStep = 3;
            _maxReachedStep = 3;
            StateHasChanged();
        }
    }

    private string GetFullPromptPreview()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Current Prompt to Improve");
        sb.AppendLine(_currentPrompt);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(_userFeedback))
        {
            sb.AppendLine("## User's Specific Requests");
            sb.AppendLine(_userFeedback);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(_feedbackInsights))
        {
            sb.AppendLine("## Insights from User Feedback");
            sb.AppendLine(_feedbackInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(_metricsInsights))
        {
            sb.AppendLine("## Insights from Evaluation Metrics");
            sb.AppendLine(_metricsInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(_sentimentInsights))
        {
            sb.AppendLine("## Insights from Sentiment Analysis");
            sb.AppendLine(_sentimentInsights);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(_messageInsights))
        {
            sb.AppendLine("## Insights from Conversation Messages");
            sb.AppendLine(_messageInsights);
            sb.AppendLine();
        }

        sb.AppendLine("Generate an improved version of the prompt that addresses the insights above.");

        return sb.ToString();
    }

    private string GetDividerStyle(int stepIndex)
    {
        var opacity = stepIndex < _maxReachedStep ? "1" : "0.3";
        return $"width: 60px; opacity: {opacity};";
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
