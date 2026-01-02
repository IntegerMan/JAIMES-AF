using MattEland.Jaimes.Web.Components.Dialogs;
using MudBlazor;

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
        if (string.IsNullOrEmpty(AgentId) || _agent == null) return;

        var version = _versions?.FirstOrDefault(v => v.Id == versionId);

        var parameters = new DialogParameters
        {
            { "AgentId", AgentId },
            { "VersionId", versionId },
            { "AgentName", _agent.Name },
            { "VersionNumber", version?.VersionNumber }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<RunTestsDialog>("Run Test Cases", parameters, options);
        var result = await dialog.Result;

        if (result is
            {
                Canceled: false, Data: MattEland.Jaimes.ServiceDefinitions.Responses.TestRunResultResponse testResult
            })
        {
            // Navigate to test results page
            if (!string.IsNullOrEmpty(testResult.ExecutionName))
            {
                NavigationManager.NavigateTo($"/admin/test-runs/{Uri.EscapeDataString(testResult.ExecutionName)}");
            }
        }
    }
}
