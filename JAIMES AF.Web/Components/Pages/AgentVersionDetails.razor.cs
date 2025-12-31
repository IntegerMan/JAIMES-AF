using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class AgentVersionDetails
{
    [Parameter] public string? AgentId { get; set; }
    [Parameter] public int VersionId { get; set; }

    private AgentInstructionVersionResponse? _version;
    private string? _agentName;
    private bool _isLoading = true;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem("Agent", href: $"/agents/{AgentId}"),
            new BreadcrumbItem("Version Details", href: null, disabled: true)
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
            // Load agent details for name
            var agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{AgentId}");
            _agentName = agent?.Name;

            // Load version details
            _version = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                $"/agents/{AgentId}/instruction-versions/{VersionId}");

            if (_version != null && _agentName != null)
            {
                // Update breadcrumbs with proper names
                _breadcrumbs = new List<BreadcrumbItem>
                {
                    new BreadcrumbItem("Home", href: "/"),
                    new BreadcrumbItem("Admin", href: "/admin"),
                    new BreadcrumbItem("Agents", href: "/agents"),
                    new BreadcrumbItem(_agentName, href: $"/agents/{AgentId}"),
                    new BreadcrumbItem($"Version {_version.VersionNumber}", href: null, disabled: true)
                };
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _errorMessage = "Agent version not found.";
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentVersionDetails")
                .LogError(ex, "Failed to load agent version details from API");
            _errorMessage = "Failed to load version details: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}
