using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class TestCaseDetails
{
    [Parameter] public int TestCaseId { get; set; }

    private TestCaseResponse? _testCase;
    private List<TestCaseRunResponse>? _runs;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnParametersSetAsync()
    {
        await LoadTestCaseAsync();
    }

    private void UpdateBreadcrumbs()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Test Cases", href: "/admin/test-cases"),
            new BreadcrumbItem(_testCase?.Name ?? $"Test Case {TestCaseId}", href: null, disabled: true)
        };
    }

    private async Task LoadTestCaseAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _testCase = await Http.GetFromJsonAsync<TestCaseResponse>($"/test-cases/{TestCaseId}");
            UpdateBreadcrumbs();

            // Load runs for this test case
            _runs = await Http.GetFromJsonAsync<List<TestCaseRunResponse>>($"/test-case-runs?testCaseId={TestCaseId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _errorMessage = "Test case not found.";
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("TestCaseDetails").LogError(ex, "Failed to load test case from API");
            _errorMessage = "Failed to load test case: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    // Title editing state
    private string? _editableTitle;
    private bool _isEditingTitle = false;
    private bool _isSavingTitle = false;

    private void EnterTitleEditMode()
    {
        _editableTitle = _testCase?.Name;
        _isEditingTitle = true;
        StateHasChanged();
    }

    private void CancelTitleEdit()
    {
        _isEditingTitle = false;
        _editableTitle = _testCase?.Name;
        StateHasChanged();
    }

    private async Task SaveTitleAndExitEditModeAsync()
    {
        if (_testCase == null || string.IsNullOrWhiteSpace(_editableTitle)) return;

        _isSavingTitle = true;
        try
        {
            var request = new MattEland.Jaimes.ServiceDefinitions.Requests.UpdateTestCaseRequest
            {
                Name = _editableTitle,
                Description = _testCase.Description
            };

            var response = await Http.PutAsJsonAsync($"/test-cases/{TestCaseId}", request);

            if (response.IsSuccessStatusCode)
            {
                var updated = await response.Content.ReadFromJsonAsync<TestCaseResponse>();
                if (updated != null)
                {
                    _testCase = updated;
                    UpdateBreadcrumbs();
                }
            }
            else
            {
                LoggerFactory.CreateLogger("TestCaseDetails")
                    .LogError("Failed to save title: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("TestCaseDetails").LogError(ex, "Failed to save test case title");
        }
        finally
        {
            _isSavingTitle = false;
            _isEditingTitle = false;
            StateHasChanged();
        }
    }
}
