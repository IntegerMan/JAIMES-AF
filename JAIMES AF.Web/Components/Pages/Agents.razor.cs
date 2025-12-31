using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Agents
{
    private AgentResponse[]? _agents;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Agents", href: null, disabled: true)
        };
        await LoadAgentsAsync();
    }

    private async Task LoadAgentsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            AgentListResponse? resp = await Http.GetFromJsonAsync<AgentListResponse>("/agents");
            _agents = resp?.Agents ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Agents").LogError(ex, "Failed to load agents from API");
            _errorMessage = "Failed to load agents: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteAgent(AgentResponse agent)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Delete Agent",
            $"Are you sure you want to delete agent '{agent.Name}'?",
            yesText: "Delete", cancelText: "Cancel");

        if (result == true)
        {
            try
            {
                await Http.DeleteAsync($"/agents/{agent.Id}");
                await LoadAgentsAsync();
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("Agents").LogError(ex, "Failed to delete agent");
                _errorMessage = "Failed to delete agent: " + ex.Message;
                StateHasChanged();
            }
        }
    }
}
