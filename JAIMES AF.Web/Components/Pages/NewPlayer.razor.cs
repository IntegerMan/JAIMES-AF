using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewPlayer
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] rulesets = [];
    private string playerId = string.Empty;
    private string? selectedRulesetId;
    private string name = string.Empty;
    private string? description;
    private bool isLoading = true;
    private bool isSaving = false;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            RulesetListResponse? response = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            rulesets = response?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewPlayer").LogError(ex, "Failed to load rulesets from API");
            errorMessage = "Failed to load rulesets: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(playerId) &&
               !string.IsNullOrWhiteSpace(selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(name);
    }

    private async Task CreatePlayerAsync()
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
            CreatePlayerRequest request = new()
            {
                Id = playerId,
                RulesetId = selectedRulesetId!,
                Description = description,
                Name = name
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/players", request);

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

                errorMessage = $"Failed to create player: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewPlayer").LogError(ex, "Failed to create player");
            errorMessage = "Failed to create player: " + ex.Message;
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

