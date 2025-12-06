namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Scenarios
{
    private ScenarioInfoResponse[]? _scenarios;
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadScenariosAsync();
    }

    private async Task LoadScenariosAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            ScenarioListResponse? resp = await Http.GetFromJsonAsync<ScenarioListResponse>("/scenarios");
            _scenarios = resp?.Scenarios ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Scenarios").LogError(ex, "Failed to load scenarios from API");
            _errorMessage = "Failed to load scenarios: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}