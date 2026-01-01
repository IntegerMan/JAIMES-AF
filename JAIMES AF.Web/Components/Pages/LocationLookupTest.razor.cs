using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class LocationLookupTest
{
	[Inject] public HttpClient Http { get; set; } = null!;

	[Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

	private GameInfoResponse[] _games = [];
	private Guid? _selectedGameId;
	private string? _locationName;
	private bool _isLoading = false;
	private string? _errorMessage;
	private string? _toolOutput;
	private List<BreadcrumbItem> _breadcrumbs = new();

	protected override async Task OnInitializedAsync()
	{
		_breadcrumbs = new List<BreadcrumbItem>
		{
			new BreadcrumbItem("Home", href: "/"),
			new BreadcrumbItem("Tools", href: null, disabled: true),
			new BreadcrumbItem("Location Lookup", href: null, disabled: true)
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
			LoggerFactory.CreateLogger("LocationLookupTest").LogError(ex, "Failed to load games from API");
			_errorMessage = "Failed to load games: " + ex.Message;
		}
	}

	private async Task LookupLocationAsync()
	{
		if (!_selectedGameId.HasValue || string.IsNullOrWhiteSpace(_locationName))
		{
			_errorMessage = "Please select a game and enter a location name.";
			return;
		}

		_isLoading = true;
		_errorMessage = null;
		_toolOutput = null;

		try
		{
			LocationResponse? location = await Http.GetFromJsonAsync<LocationResponse>(
				$"/games/{_selectedGameId.Value}/locations/by-name/{Uri.EscapeDataString(_locationName)}");

			if (location == null)
			{
				_toolOutput = $"Location '{_locationName}' was not found in this game.";
			}
			else
			{
				_toolOutput = FormatLocationOutput(location);
			}
		}
		catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			_toolOutput = $"Location '{_locationName}' was not found in this game.";
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("LocationLookupTest").LogError(ex, "Failed to lookup location");
			_errorMessage = "Failed to lookup location: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private async Task ListAllLocationsAsync()
	{
		if (!_selectedGameId.HasValue)
		{
			_errorMessage = "Please select a game.";
			return;
		}

		_isLoading = true;
		_errorMessage = null;
		_toolOutput = null;

		try
		{
			LocationListResponse? response = await Http.GetFromJsonAsync<LocationListResponse>(
				$"/games/{_selectedGameId.Value}/locations");

			if (response == null || response.TotalCount == 0)
			{
				_toolOutput = "No locations have been established in this game yet.";
			}
			else
			{
				_toolOutput = $"Known locations ({response.TotalCount}):\n\n" +
				              string.Join("\n\n", response.Locations.Select(l =>
					              $"**{l.Name}**: {l.Description}" + (l.EventCount > 0 ? $" ({l.EventCount} event(s) recorded)" : "")));
			}
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("LocationLookupTest").LogError(ex, "Failed to list locations");
			_errorMessage = "Failed to list locations: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private static string FormatLocationOutput(LocationResponse location)
	{
		List<string> parts =
		[
			$"**{location.Name}**",
			$"Description: {location.Description}"
		];

		if (!string.IsNullOrWhiteSpace(location.Appearance))
		{
			parts.Add($"Appearance: {location.Appearance}");
		}

		if (!string.IsNullOrWhiteSpace(location.StorytellerNotes))
		{
			parts.Add($"[Storyteller Notes - Hidden from player]: {location.StorytellerNotes}");
		}

		parts.Add($"\nEvents: {location.EventCount}");
		parts.Add($"Nearby Locations: {location.NearbyLocationCount}");

		return string.Join("\n", parts);
	}

	private string FormatGameDisplay(GameInfoResponse game)
	{
		return $"{game.PlayerName} - {game.ScenarioName} ({game.RulesetName})";
	}
}
