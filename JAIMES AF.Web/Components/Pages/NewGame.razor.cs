using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Requests;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewGame
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private ScenarioInfoResponse[] scenarios = [];
    private PlayerInfoResponse[] players = [];
    private string? selectedScenarioId;
    private string? selectedPlayerId;
    private bool isLoading = true;
    private bool isCreating = false;
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
            var scenariosTask = Http.GetFromJsonAsync<ScenarioListResponse>("/scenarios");
            var playersTask = Http.GetFromJsonAsync<PlayerListResponse>("/players");

            await Task.WhenAll(scenariosTask, playersTask);

            var scenariosResponse = await scenariosTask;
            var playersResponse = await playersTask;

            scenarios = scenariosResponse?.Scenarios ?? [];
            players = playersResponse?.Players ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewGame").LogError(ex, "Failed to load scenarios or players from API");
            errorMessage = "Failed to load scenarios or players: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task CreateGameAsync()
    {
        if (string.IsNullOrEmpty(selectedScenarioId) || string.IsNullOrEmpty(selectedPlayerId))
        {
            errorMessage = "Please select both a scenario and a player.";
            StateHasChanged();
            return;
        }

        isCreating = true;
        errorMessage = null;
        try
        {
            NewGameRequest request = new()
            {
                ScenarioId = selectedScenarioId,
                PlayerId = selectedPlayerId
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
                    errorMessage = "Game was created but the response was invalid.";
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

                errorMessage = $"Failed to create game: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewGame").LogError(ex, "Failed to create game");
            errorMessage = "Failed to create game: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            isCreating = false;
            StateHasChanged();
        }
    }
}

