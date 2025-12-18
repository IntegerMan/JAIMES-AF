using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class AgentInstructionVersions
{
    [Parameter] public string? AgentId { get; set; }

    private AgentInstructionVersionResponse[]? _versions;
    private bool _isLoading = true;
    private string? _errorMessage;
    private string _agentId = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _agentId = AgentId ?? string.Empty;
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
            AgentInstructionVersionListResponse? resp = await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>($"/agents/{_agentId}/instruction-versions");
            _versions = resp?.InstructionVersions ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentInstructionVersions").LogError(ex, "Failed to load instruction versions from API");
            _errorMessage = "Failed to load instruction versions: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}
