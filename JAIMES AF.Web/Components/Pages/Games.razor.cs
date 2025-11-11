using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

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

    private async Task DeleteGameAsync(Guid gameId)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Delete Game",
            "Are you sure you want to delete this game? This action cannot be undone.",
            yesText: "Delete",
            cancelText: "Cancel");

        if (result == true)
        {
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/games/{gameId}");
                if (response.IsSuccessStatusCode)
                {
                    await LoadGamesAsync();
                }
                else
                {
                    LoggerFactory.CreateLogger("Games").LogError("Failed to delete game: {StatusCode}", response.StatusCode);
                    errorMessage = "Failed to delete game. Please try again.";
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to delete game from API");
                errorMessage = "Failed to delete game: " + ex.Message;
                StateHasChanged();
            }
        }
    }
}