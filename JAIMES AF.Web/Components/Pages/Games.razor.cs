using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Games
{
    private GameInfoResponse[]? games;
    private bool isLoading = true;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            var resp = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
            games = resp?.Games ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to load games from API");
            errorMessage = "Failed to load games: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void OpenGame(string gameId)
    {
        Nav.NavigateTo($"/games/{gameId}");
    }
}