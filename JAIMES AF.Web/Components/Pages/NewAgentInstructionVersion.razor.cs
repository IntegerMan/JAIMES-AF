using MattEland.Jaimes.ServiceDefinitions.Requests;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewAgentInstructionVersion
{
    [Parameter] public string AgentId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private string _versionNumber = string.Empty;
    private string _instructions = string.Empty;
    private bool _isActive = true; // Default to active for new versions
    private bool _isSaving = false;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem($"Agent {AgentId}", href: null, disabled: true),
            new BreadcrumbItem("Instructions", href: $"/agents/{AgentId}/instruction-versions"),
            new BreadcrumbItem("New", href: null, disabled: true)
        };

        // Initialize with suggested version number
        _versionNumber = $"v{DateTime.Now:yyyy.MM.dd}";
    }

    private List<BreadcrumbItem> _breadcrumbs = new();

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_versionNumber) &&
               !string.IsNullOrWhiteSpace(_instructions);
    }

    private async Task CreateInstructionVersionAsync()
    {
        if (!IsFormValid())
        {
            _errorMessage = "Please fill in all required fields.";
            StateHasChanged();
            return;
        }

        _isSaving = true;
        _errorMessage = null;
        try
        {
            CreateAgentInstructionVersionRequest request = new()
            {
                VersionNumber = _versionNumber,
                Instructions = _instructions
            };

            HttpResponseMessage response =
                await Http.PostAsJsonAsync($"/agents/{AgentId}/instruction-versions", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo($"/agents/{AgentId}/instruction-versions");
            }
            else
            {
                string? body = null;
                try
                {
                    body = await response.Content.ReadAsStringAsync();
                }
                catch
                {
                    // ignored
                }

                _errorMessage =
                    $"Failed to create instruction version: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewAgentInstructionVersion")
                .LogError(ex, "Failed to create instruction version");
            _errorMessage = "Failed to create instruction version: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        Navigation.NavigateTo($"/agents/{AgentId}/instruction-versions");
    }
}