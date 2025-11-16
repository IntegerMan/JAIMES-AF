using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Scenarios
{
    private ScenarioInfoResponse[]? scenarios;
    private bool isLoading = true;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadScenariosAsync();
    }

    private async Task LoadScenariosAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            ScenarioListResponse? resp = await Http.GetFromJsonAsync<ScenarioListResponse>("/scenarios");
            scenarios = resp?.Scenarios ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Scenarios").LogError(ex, "Failed to load scenarios from API");
            errorMessage = "Failed to load scenarios: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}

