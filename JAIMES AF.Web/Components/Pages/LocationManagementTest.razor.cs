using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class LocationManagementTest
{
	[Inject] public HttpClient Http { get; set; } = null!;
	[Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

	private GameInfoResponse[] _games = [];
	private LocationResponse[] _locations = [];
	private Guid? _selectedGameId;
	private bool _isLoading = false;
	private string? _errorMessage;
	private string? _successMessage;
	private List<BreadcrumbItem> _breadcrumbs = new();

	protected override async Task OnInitializedAsync()
	{
		_breadcrumbs = new List<BreadcrumbItem>
		{
			new BreadcrumbItem("Home", href: "/"),
			new BreadcrumbItem("Tools", href: null, disabled: true),
			new BreadcrumbItem("Location Management", href: null, disabled: true)
		};

		await LoadGamesAsync();
	}

	private async Task LoadGamesAsync()
	{
		try
		{
			ListGamesResponse? response = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
			_games = response?.Games ?? [];
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("LocationManagementTest").LogError(ex, "Failed to load games from API");
			_errorMessage = "Failed to load games: " + ex.Message;
		}
	}

	private async Task RefreshLocationsAsync()
	{
		if (!_selectedGameId.HasValue) return;

		_isLoading = true;
		_errorMessage = null;
		_successMessage = null;

		try
		{
			LocationListResponse? response = await Http.GetFromJsonAsync<LocationListResponse>(
				$"/games/{_selectedGameId.Value}/locations");
			_locations = response?.Locations ?? [];
			_successMessage = $"Loaded {_locations.Length} location(s).";
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("LocationManagementTest").LogError(ex, "Failed to load locations");
			_errorMessage = "Failed to load locations: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private string FormatGameDisplay(GameInfoResponse game)
	{
		return $"{game.PlayerName} - {game.ScenarioName} ({game.RulesetName})";
	}
}
