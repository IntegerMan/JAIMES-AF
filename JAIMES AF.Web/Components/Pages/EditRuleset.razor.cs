using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditRuleset
{
    [Parameter]
    public string RulesetId { get; set; } = string.Empty;

    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private string name = string.Empty;
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
            RulesetResponse? rulesetResponse = await Http.GetFromJsonAsync<RulesetResponse>($"/rulesets/{RulesetId}");

            if (rulesetResponse == null)
            {
                errorMessage = $"Ruleset with ID '{RulesetId}' not found.";
                isLoading = false;
                StateHasChanged();
                return;
            }

            name = rulesetResponse.Name;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditRuleset").LogError(ex, "Failed to load ruleset from API");
            errorMessage = "Failed to load ruleset: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(name);
    }

    private async Task UpdateRulesetAsync()
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
            UpdateRulesetRequest request = new()
            {
                Name = name
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/rulesets/{RulesetId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/rulesets");
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

                errorMessage = $"Failed to update ruleset: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditRuleset").LogError(ex, "Failed to update ruleset");
            errorMessage = "Failed to update ruleset: " + ex.Message;
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
        Navigation.NavigateTo("/rulesets");
    }
}

