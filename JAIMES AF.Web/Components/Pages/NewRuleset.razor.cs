using MattEland.Jaimes.ServiceDefinitions.Requests;
using Microsoft.AspNetCore.Components;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewRuleset
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private string rulesetId = string.Empty;
    private string name = string.Empty;
    private bool isSaving = false;
    private string? errorMessage;

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(rulesetId) &&
               !string.IsNullOrWhiteSpace(name);
    }

    private async Task CreateRulesetAsync()
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
            CreateRulesetRequest request = new()
            {
                Id = rulesetId,
                Name = name
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/rulesets", request);

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

                errorMessage = $"Failed to create ruleset: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewRuleset").LogError(ex, "Failed to create ruleset");
            errorMessage = "Failed to create ruleset: " + ex.Message;
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

