namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Games
{
    private GameInfoResponse[]? _games;
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            ListGamesResponse? resp = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
            _games = resp?.Games ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to load games from API");
            _errorMessage = "Failed to load games: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteGameAsync(Guid gameId)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Delete Game",
            "Are you sure you want to delete this game? This action cannot be undone.",
            "Delete",
            cancelText: "Cancel");

        if (result == true)
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/games/{gameId}");
                if (response.IsSuccessStatusCode)
                {
                    await LoadGamesAsync();
                }
                else
                {
                    LoggerFactory.CreateLogger("Games")
                        .LogError("Failed to delete game: {StatusCode}", response.StatusCode);
                    _errorMessage = "Failed to delete game. Please try again.";
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to delete game from API");
                _errorMessage = "Failed to delete game: " + ex.Message;
                StateHasChanged();
            }
    }
}