using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditAgent
{
    [Parameter] public string AgentId { get; set; } = string.Empty;

    [Parameter] [SupplyParameterFromQuery] public int? BaseVersionId { get; set; }

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

            // Load instructions - either from base version (if specified) or active version
            try
            {
                AgentInstructionVersionResponse? versionToLoad = null;

                if (BaseVersionId.HasValue)
                {
                    versionToLoad = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                        $"/agents/{AgentId}/instruction-versions/{BaseVersionId}");
                }
                else
                {
                    versionToLoad = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                        $"/agents/{AgentId}/instruction-versions/active");
                }

                if (versionToLoad != null)
                {
                    _instructions = versionToLoad.Instructions;
                    // If we are basing off a specific version, we treat it as "new" content for the purpose of the form,
                    // but we track original as active instructions for change detection if we were loading active.
                    // Actually, simpler logic: 
                    // If loading active, _original matches loaded.
                    // If loading specific, _original matches loaded too? 
                    // No, if I change nothing and click save, it shouldn't create a new version if it matches ACTIVE.
                    // But if I load an OLD version, and save, it SHOULD create a new version even if it matches the old version (because the old version is not active).
                    // So we probably want to load the ACTIVE version to set _originalInstructions for comparison, 
                    // but set _instructions to the BaseVersionId content.

                    if (BaseVersionId.HasValue)
                    {
                        // We are loading a specific version to edit. 
                        // We should check what the ACTIVE version is to set _originalInstructions correctly for "no change" detection?
                        // If I load v1 (which is old), and active is v2. 
                        // v1 text != v2 text.
                        // I want _instructions = v1 text.
                        // If I click save, it compares _instructions vs _originalInstructions.
                        // If _originalInstructions is v1 text, then no change detected -> no new version.
                        // But we WANT a new version if v1 != v2 (active).

                        // Actually, the requirement is "bases the new version off of this version's settings".
                        // Usually this implies "I want to revert to this version" or "start editing from here".
                        // In either case, if I just click save, I probably expect it to become the new active version.
                        // So we should force a new version creation if BaseVersionId is present, UNLESS it happens to be identical to the CURRENT active version.

                        // Let's grab active version too to check against.
                        try
                        {
                            var active = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                                $"/agents/{AgentId}/instruction-versions/active");
                            _originalInstructions = active?.Instructions ?? string.Empty;
                        }
                        catch
                        {
                            _originalInstructions = string.Empty;
                        }
                    }
                    else
                    {
                        // Standard edit flow
                        _originalInstructions = versionToLoad.Instructions;
                    }

                    _instructions = versionToLoad.Instructions;
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                if (!BaseVersionId.HasValue)
                {
                    // Agent has no active instruction version - create a default one
                    var logger = LoggerFactory.CreateLogger("EditAgent");
                    logger.LogWarning("Agent {AgentId} has no active instruction version, creating default one",
                        AgentId);

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
                else
                {
                    _errorMessage = $"Version {BaseVersionId} not found.";
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