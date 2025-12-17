using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditAgentInstructionVersion
{
    [Parameter] public string AgentId { get; set; } = string.Empty;

    [Parameter] public string InstructionVersionId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private string _versionNumber = string.Empty;
    private string _instructions = string.Empty;
    private bool _isActive = false;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadInstructionVersionAsync();
    }

    private async Task LoadInstructionVersionAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            if (!int.TryParse(InstructionVersionId, out int id))
            {
                _errorMessage = "Invalid instruction version ID.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            var version = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>($"/agents/{AgentId}/instruction-versions/{id}");
            if (version == null)
            {
                _errorMessage = $"Instruction version with ID '{InstructionVersionId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _versionNumber = version.VersionNumber;
            _instructions = version.Instructions;
            _isActive = version.IsActive;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditAgentInstructionVersion").LogError(ex, "Failed to load instruction version from API");
            _errorMessage = "Failed to load instruction version: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_versionNumber) &&
               !string.IsNullOrWhiteSpace(_instructions);
    }

    private async Task UpdateInstructionVersionAsync()
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
            UpdateAgentInstructionVersionRequest request = new()
            {
                VersionNumber = _versionNumber,
                Instructions = _instructions,
                IsActive = _isActive
            };

            if (!int.TryParse(InstructionVersionId, out int id))
            {
                _errorMessage = "Invalid instruction version ID.";
                StateHasChanged();
                return;
            }

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/agents/{AgentId}/instruction-versions/{id}", request);

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
                    $"Failed to update instruction version: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditAgentInstructionVersion").LogError(ex, "Failed to update instruction version");
            _errorMessage = "Failed to update instruction version: " + ex.Message;
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