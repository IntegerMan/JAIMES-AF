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

    protected override async Task OnInitializedAsync()
    {
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

            // Load the active instruction version for editing
            var activeVersion = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>($"/agents/{AgentId}/instruction-versions/active");
            if (activeVersion != null)
            {
                _instructions = activeVersion.Instructions;
                _originalInstructions = activeVersion.Instructions;
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

                HttpResponseMessage versionResponse = await Http.PostAsJsonAsync($"/agents/{AgentId}/instruction-versions", versionRequest);
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
                Navigation.NavigateTo("/agents");
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
        Navigation.NavigateTo("/agents");
    }
}