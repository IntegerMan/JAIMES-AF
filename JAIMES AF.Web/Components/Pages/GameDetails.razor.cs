using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameDetails
{
    [Parameter] public Guid GameId { get; set; }

    private GameInfoResponse? game;
    private bool isLoading = true;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadGameAsync();
    }

    private async Task LoadGameAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            var resp = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
            game = resp?.Games?.FirstOrDefault(g => g.GameId == GameId);
            if (game == null)
            {
                game = new GameInfoResponse
                {
                    GameId = GameId, ScenarioId = "unknown", ScenarioName = "(Unknown)", RulesetId = "unknown",
                    RulesetName = "(Unknown)", PlayerId = "unknown", PlayerName = "(Unknown)"
                };
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameDetails").LogError(ex, "Failed to load game info");
            errorMessage = "Failed to load game: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}