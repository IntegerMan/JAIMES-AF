using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class AgentInstructionVersions
{
    [Parameter] public string? AgentId { get; set; }

    private AgentInstructionVersionResponse[]? _versions;
    private bool _isLoading = true;
    private string? _errorMessage;
    private string _agentId = string.Empty;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _agentId = AgentId ?? string.Empty;

        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem($"Agent {AgentId}", href: null, disabled: true),
            new BreadcrumbItem("Instructions", href: null, disabled: true)
        };

        await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        if (string.IsNullOrEmpty(_agentId))
        {
            _errorMessage = "Agent ID is required";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _errorMessage = null;
        try
        {
            AgentInstructionVersionListResponse? resp =
                await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                    $"/agents/{_agentId}/instruction-versions");
            _versions = resp?.InstructionVersions ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentInstructionVersions")
                .LogError(ex, "Failed to load instruction versions from API");
            _errorMessage = "Failed to load instruction versions: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}

