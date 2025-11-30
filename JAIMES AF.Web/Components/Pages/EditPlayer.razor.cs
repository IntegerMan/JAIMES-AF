using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditPlayer
{
    [Parameter]
    public string PlayerId { get; set; } = string.Empty;

    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] rulesets = [];
    private string? selectedRulesetId;
    private string name = string.Empty;
    private string? description;
    private bool isLoading = true;
    private bool isSaving = false;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            Task<RulesetListResponse?> rulesetsTask = Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            Task<PlayerResponse?> playerTask = Http.GetFromJsonAsync<PlayerResponse>($"/players/{PlayerId}");

            await Task.WhenAll(rulesetsTask, playerTask);

            RulesetListResponse? rulesetsResponse = await rulesetsTask;
            PlayerResponse? playerResponse = await playerTask;

            if (playerResponse == null)
            {
                errorMessage = $"Player with ID '{PlayerId}' not found.";
                isLoading = false;
                StateHasChanged();
                return;
            }

            rulesets = rulesetsResponse?.Rulesets ?? [];
            selectedRulesetId = playerResponse.RulesetId;
            name = playerResponse.Name;
            description = playerResponse.Description;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditPlayer").LogError(ex, "Failed to load player or rulesets from API");
            errorMessage = "Failed to load player: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(name);
    }

    private async Task UpdatePlayerAsync()
    {
        if (!IsFormValid())
        {
            errorMessage = "Please fill in all required fields.";
            StateHasChanged();
            return;
        }

        isSaving = true;
        errorMessage = null;
        try
        {
            UpdatePlayerRequest request = new()
            {
                RulesetId = selectedRulesetId!,
                Description = description,
                Name = name
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/players/{PlayerId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/players");
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

                errorMessage = $"Failed to update player: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditPlayer").LogError(ex, "Failed to update player");
            errorMessage = "Failed to update player: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/players");
    }
}

