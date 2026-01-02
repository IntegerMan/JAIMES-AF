using MattEland.Jaimes.ServiceDefinitions.Responses;
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

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
