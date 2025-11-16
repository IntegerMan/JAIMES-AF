using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Rulesets
{
    private RulesetInfoResponse[]? rulesets;
    private bool isLoading = true;
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
            RulesetListResponse? resp = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            rulesets = resp?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Rulesets").LogError(ex, "Failed to load rulesets from API");
            errorMessage = "Failed to load rulesets: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}

