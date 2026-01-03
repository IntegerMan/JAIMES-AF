using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Web.Components.Dialogs;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class TestCases
{
    private List<TestCaseResponse>? _testCases;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Test Cases", href: null, disabled: true)
        };
        await LoadTestCasesAsync();
    }

    private async Task LoadTestCasesAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _testCases = await Http.GetFromJsonAsync<List<TestCaseResponse>>("/test-cases?includeInactive=true");
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("TestCases").LogError(ex, "Failed to load test cases from API");
            _errorMessage = "Failed to load test cases: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeactivateTestCaseAsync(int testCaseId)
    {
        try
        {
            var response = await Http.DeleteAsync($"/test-cases/{testCaseId}");
            if (response.IsSuccessStatusCode)
            {
                // Refresh the list
                await LoadTestCasesAsync();
            }
            else
            {
                _errorMessage = "Failed to deactivate test case";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("TestCases").LogError(ex, "Failed to deactivate test case");
            _errorMessage = "Failed to deactivate test case: " + ex.Message;
            StateHasChanged();
        }
    }

    private async Task RunAllTestsAsync()
    {
        // Open the RunTestsDialog - user will select agent/version there
        // For now, we navigate to agents page to let user pick an agent version
        // A more sophisticated approach would be a dialog that lets you pick agent + version

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        // Get the first test case's agent to use as default
        string? defaultAgentId = _testCases?.FirstOrDefault()?.AgentId;

        if (string.IsNullOrEmpty(defaultAgentId))
        {
            return;
        }

        var parameters = new DialogParameters
        {
            { "AgentId", defaultAgentId },
            { "VersionId", 0 }, // Will need to be selected in dialog
            { "AgentName", _testCases?.FirstOrDefault()?.AgentName },
            { "VersionNumber", (int?)null }
        };

        var dialog = await DialogService.ShowAsync<RunTestsDialog>("Run Test Cases", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: TestRunResultResponse testResult })
        {
            if (!string.IsNullOrEmpty(testResult.ExecutionName))
            {
                NavigationManager.NavigateTo($"/admin/test-runs/{Uri.EscapeDataString(testResult.ExecutionName)}");
            }
        }
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
