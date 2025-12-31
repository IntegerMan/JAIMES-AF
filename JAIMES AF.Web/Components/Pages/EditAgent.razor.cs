using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditAgent
{
    [Parameter] public string AgentId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private string _name = string.Empty;
    private string _role = string.Empty;
    private string _instructions = string.Empty;
    private string _originalInstructions = string.Empty;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem("Edit Agent", href: null, disabled: true)
        };
        await LoadAgentAsync();
    }

    private async Task LoadAgentAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            var agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{AgentId}");
            if (agent == null)
            {
                _errorMessage = $"Agent with ID '{AgentId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _name = agent.Name;
            _role = agent.Role;

            _breadcrumbs = new List<BreadcrumbItem>
            {
                new BreadcrumbItem("Home", href: "/"),
                new BreadcrumbItem("Admin", href: "/admin"),
                new BreadcrumbItem("Agents", href: "/agents"),
                new BreadcrumbItem(agent.Name, href: null, disabled: true),
                new BreadcrumbItem("Edit", href: null, disabled: true)
            };

            // Load the active instruction version for editing
            try
            {
                var activeVersion =
                    await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                        $"/agents/{AgentId}/instruction-versions/active");
                if (activeVersion != null)
                {
                    _instructions = activeVersion.Instructions;
                    _originalInstructions = activeVersion.Instructions;
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                // Agent has no active instruction version - create a default one
                var logger = LoggerFactory.CreateLogger("EditAgent");
                logger.LogWarning("Agent {AgentId} has no active instruction version, creating default one", AgentId);

                var defaultInstructions =
                    $"You are {agent.Name}, a {agent.Role}. Provide helpful and engaging responses.";
                var createVersionRequest = new CreateAgentInstructionVersionRequest
                {
                    VersionNumber = "v1.0",
                    Instructions = defaultInstructions
                };

                var createResponse =
                    await Http.PostAsJsonAsync($"/agents/{AgentId}/instruction-versions", createVersionRequest);
                if (createResponse.IsSuccessStatusCode)
                {
                    var createdVersion =
                        await createResponse.Content.ReadFromJsonAsync<AgentInstructionVersionResponse>();
                    if (createdVersion != null)
                    {
                        _instructions = createdVersion.Instructions;
                        _originalInstructions = createdVersion.Instructions;
                    }
                }
                else
                {
                    _errorMessage =
                        "Agent has no instruction versions and failed to create a default one. Please create an instruction version manually.";
                }
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditAgent").LogError(ex, "Failed to load agent from API");
            _errorMessage = "Failed to load agent: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_name) &&
               !string.IsNullOrWhiteSpace(_role) &&
               !string.IsNullOrWhiteSpace(_instructions);
    }

    private async Task UpdateAgentAsync()
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
            // Check if instructions changed
            bool instructionsChanged = _instructions != _originalInstructions;

            // If instructions changed, create a new instruction version
            if (instructionsChanged)
            {
                string versionNumber = $"v{DateTime.Now:yyyy.MM.dd.HH.mm}";
                CreateAgentInstructionVersionRequest versionRequest = new()
                {
                    VersionNumber = versionNumber,
                    Instructions = _instructions
                };

                HttpResponseMessage versionResponse =
                    await Http.PostAsJsonAsync($"/agents/{AgentId}/instruction-versions", versionRequest);
                if (!versionResponse.IsSuccessStatusCode)
                {
                    _errorMessage = "Failed to create new instruction version.";
                    StateHasChanged();
                    return;
                }
            }

            // Update agent basic info
            UpdateAgentRequest request = new()
            {
                Name = _name,
                Role = _role
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/agents/{AgentId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo($"/agents/{AgentId}");
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
                    $"Failed to update agent: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditAgent").LogError(ex, "Failed to update agent");
            _errorMessage = "Failed to update agent: " + ex.Message;
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
        Navigation.NavigateTo($"/agents/{AgentId}");
    }
}