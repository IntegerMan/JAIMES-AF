using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Locations
{
    [Inject] public required IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] public required ILoggerFactory LoggerFactory { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    private List<BreadcrumbItem> _breadcrumbs = [];
    private GameInfoResponse[] _games = [];
    private LocationResponse[] _locations = [];
    private Guid? _selectedGameId;
    private bool _isLoading = true;
    private string? _errorMessage;

    private Guid? SelectedGameId
    {
        get => _selectedGameId;
        set
        {
            if (_selectedGameId != value)
            {
                _selectedGameId = value;
                _ = LoadLocationsAsync();
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs =
        [
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Locations", href: null, disabled: true)
        ];

        await LoadGamesAsync();
        await LoadLocationsAsync();
    }

    private HttpClient CreateClient() => HttpClientFactory.CreateClient("Api");

    private async Task LoadGamesAsync()
    {
        try
        {
            ListGamesResponse? response = await CreateClient().GetFromJsonAsync<ListGamesResponse>("/games");
            _games = response?.Games ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Locations").LogError(ex, "Failed to load games");
        }
    }

    private async Task LoadLocationsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            HttpClient client = CreateClient();

            if (_selectedGameId.HasValue)
            {
                LocationListResponse? response = await client.GetFromJsonAsync<LocationListResponse>(
                    $"/games/{_selectedGameId.Value}/locations");
                _locations = response?.Locations ?? [];
            }
            else
            {
                // Load from all games
                List<LocationResponse> allLocations = [];
                foreach (var game in _games)
                {
                    try
                    {
                        LocationListResponse? response = await client.GetFromJsonAsync<LocationListResponse>(
                            $"/games/{game.GameId}/locations");
                        if (response?.Locations != null)
                        {
                            allLocations.AddRange(response.Locations);
                        }
                    }
                    catch
                    {
                        // Continue loading other games
                    }
                }

                _locations = allLocations.OrderBy(l => l.Name).ToArray();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Locations").LogError(ex, "Failed to load locations");
            _errorMessage = "Failed to load locations: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OpenNewLocationDialog()
    {
        if (!_selectedGameId.HasValue) return;

        var parameters = new DialogParameters<NewLocationDialog>
        {
            { x => x.GameId, _selectedGameId.Value }
        };

        var dialog = await DialogService.ShowAsync<NewLocationDialog>("New Location", parameters, new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        });

        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await LoadLocationsAsync();
            Snackbar.Add("Location created successfully!", Severity.Success);
        }
    }

    private static string FormatGameDisplay(GameInfoResponse game)
    {
        return $"{game.PlayerName} - {game.ScenarioName} ({game.RulesetName})";
    }

    private string GetGameName(Guid gameId)
    {
        var game = _games.FirstOrDefault(g => g.GameId == gameId);
        return game != null ? FormatGameDisplay(game) : gameId.ToString()[..8] + "...";
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "-";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }
}
