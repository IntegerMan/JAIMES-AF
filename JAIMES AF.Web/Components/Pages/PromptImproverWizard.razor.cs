using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class PromptImproverWizard
{
    [Parameter] public string AgentId { get; set; } = string.Empty;
    [Parameter] public int? VersionId { get; set; }

    [Inject] public IJSRuntime JS { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private int _activeStep;

    // Current prompt
    private string _currentPrompt = string.Empty;
    private int _effectiveVersionId;

    // Insights
    private string? _feedbackInsights;
    private string? _metricsInsights;
    private string? _sentimentInsights;
    private bool _isGeneratingFeedback;
    private bool _isGeneratingMetrics;
    private bool _isGeneratingSentiment;

    // User feedback
    private string _userFeedback = string.Empty;

    // Improved prompt
    private string _improvedPrompt = string.Empty;
    private bool _isGeneratingPrompt;

    private bool HasAnyInsights => !string.IsNullOrEmpty(_feedbackInsights) ||
                                   !string.IsNullOrEmpty(_metricsInsights) ||
                                   !string.IsNullOrEmpty(_sentimentInsights);

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

    private async Task GenerateFeedbackInsightsAsync()
    {
        _isGeneratingFeedback = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "feedback" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>();
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
                Snackbar.Add("Failed to generate feedback insights", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingFeedback = false;
            StateHasChanged();
        }
    }

    private async Task GenerateMetricsInsightsAsync()
    {
        _isGeneratingMetrics = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "metrics" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>();
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
                Snackbar.Add("Failed to generate metrics insights", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingMetrics = false;
            StateHasChanged();
        }
    }

    private async Task GenerateSentimentInsightsAsync()
    {
        _isGeneratingSentiment = true;
        StateHasChanged();

        try
        {
            var request = new GenerateInsightsRequest { InsightType = "sentiment" };
            var response = await Http.PostAsJsonAsync(
                $"/agents/{AgentId}/instruction-versions/{_effectiveVersionId}/generate-insights",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateInsightsResponse>();
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
                Snackbar.Add("Failed to generate sentiment insights", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingSentiment = false;
            StateHasChanged();
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
                SentimentInsights = _sentimentInsights
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
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", _improvedPrompt);
        Snackbar.Add("Copied to clipboard!", Severity.Info);
    }

    private void ApplyImprovedPrompt()
    {
        // Navigate to edit page with the improved prompt
        // We'll use sessionStorage to pass the improved prompt
        Navigation.NavigateTo($"/agents/{AgentId}/edit?baseVersionId={_effectiveVersionId}&improvedPrompt=true");
    }

    private void RegeneratePrompt()
    {
        _improvedPrompt = string.Empty;
        StateHasChanged();
    }

    private void NextStep()
    {
        if (_activeStep < 2)
        {
            _activeStep++;
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
}
