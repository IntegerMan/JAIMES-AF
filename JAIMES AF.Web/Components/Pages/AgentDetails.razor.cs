
namespace MattEland.Jaimes.Web.Components.Pages;

public partial class AgentDetails
{
    [Parameter] public string? AgentId { get; set; }

    private AgentResponse? _agent;
    private AgentInstructionVersionResponse[]? _versions;
    private bool _isLoading = true;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnParametersSetAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem("Agent Details", href: null, disabled: true)
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
            _agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{AgentId}");

            if (_agent != null)
            {
                // Update breadcrumbs with agent name
                _breadcrumbs = new List<BreadcrumbItem>
                {
                    new BreadcrumbItem("Home", href: "/"),
                    new BreadcrumbItem("Admin", href: "/admin"),
                    new BreadcrumbItem("Agents", href: "/agents"),
                    new BreadcrumbItem(_agent.Name, href: null, disabled: true)
                };
            }

            // Load instruction versions
            AgentInstructionVersionListResponse? versionsResp =
                await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                    $"/agents/{AgentId}/instruction-versions");
            _versions = versionsResp?.InstructionVersions ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentDetails")
                .LogError(ex, "Failed to load agent details from API");
            _errorMessage = "Failed to load agent details: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task RunTestsForVersionAsync(int versionId)
    {
        if (string.IsNullOrEmpty(AgentId)) return;

        try
        {
            var request = new MattEland.Jaimes.ServiceDefinitions.Requests.RunTestCasesRequest
            {
                ExecutionName = $"Manual Test Run - {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            };

            var response = await Http.PostAsJsonAsync($"/agents/{AgentId}/versions/{versionId}/test-run", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<MattEland.Jaimes.ServiceDefinitions.Responses.TestRunResultResponse>();
                if (result != null)
                {
                    // Navigate to test cases page to see results
                    NavigationManager.NavigateTo("/admin/test-cases");
                }
            }
            else
            {
                _errorMessage = "Failed to run tests: " + await response.Content.ReadAsStringAsync();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentDetails").LogError(ex, "Failed to run tests");
            _errorMessage = "Failed to run tests: " + ex.Message;
            StateHasChanged();
        }
    }
}
