namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewGame
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private ScenarioInfoResponse[] _scenarios = [];
    private PlayerInfoResponse[] _players = [];
    private string? _selectedScenarioId;
    private string? _selectedPlayerId;
    private bool _isLoading = true;
    private bool _isCreating = false;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            Task<ScenarioListResponse?> scenariosTask = Http.GetFromJsonAsync<ScenarioListResponse>("/scenarios");
            Task<PlayerListResponse?> playersTask = Http.GetFromJsonAsync<PlayerListResponse>("/players");

            await Task.WhenAll(scenariosTask, playersTask);

            ScenarioListResponse? scenariosResponse = await scenariosTask;
            PlayerListResponse? playersResponse = await playersTask;

            _scenarios = scenariosResponse?.Scenarios ?? [];
            _players = playersResponse?.Players ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewGame").LogError(ex, "Failed to load scenarios or players from API");
            _errorMessage = "Failed to load scenarios or players: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private string GetScenarioDisplayName(ScenarioInfoResponse scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.Name)) return scenario.Name;

        if (!string.IsNullOrWhiteSpace(scenario.Description)) return scenario.Description;

        return "Unnamed Scenario";
    }

    private async Task CreateGameAsync()
    {
        if (string.IsNullOrEmpty(_selectedScenarioId) || string.IsNullOrEmpty(_selectedPlayerId))
        {
            _errorMessage = "Please select both a scenario and a player.";
            StateHasChanged();
            return;
        }

        _isCreating = true;
        _errorMessage = null;
        try
        {
            NewGameRequest request = new()
            {
                ScenarioId = _selectedScenarioId,
                PlayerId = _selectedPlayerId
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/games/", request);

            if (response.IsSuccessStatusCode)
            {
                NewGameResponse? gameResponse = await response.Content.ReadFromJsonAsync<NewGameResponse>();
                if (gameResponse != null)
                {
                    Navigation.NavigateTo($"/games/{gameResponse.GameId}");
                }
                else
                {
                    _errorMessage = "Game was created but the response was invalid.";
                    StateHasChanged();
                }
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
                    $"Failed to create game: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewGame").LogError(ex, "Failed to create game");
            _errorMessage = "Failed to create game: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            _isCreating = false;
            StateHasChanged();
        }
    }
}