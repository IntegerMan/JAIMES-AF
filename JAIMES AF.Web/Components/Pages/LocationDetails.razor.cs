using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Web.Components.Shared;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class LocationDetails
{
	[Parameter] public int LocationId { get; set; }
	[Inject] public required IHttpClientFactory HttpClientFactory { get; set; }
	[Inject] public required ILoggerFactory LoggerFactory { get; set; }
	[Inject] public required ISnackbar Snackbar { get; set; }

	private List<BreadcrumbItem> _breadcrumbs = [];
	private LocationResponse? _location;
	private LocationEventResponse[] _events = [];
	private NearbyLocationResponse[] _nearbyLocations = [];
	private bool _isLoading = true;
	private string? _errorMessage;

	// Add event form
	private bool _showAddEventForm;
	private string _newEventName = string.Empty;
	private string _newEventDescription = string.Empty;
	private bool _isAddingEvent;

	protected override async Task OnInitializedAsync()
	{
		await LoadLocationAsync();
	}

	protected override async Task OnParametersSetAsync()
	{
		await LoadLocationAsync();
	}

	private HttpClient CreateClient() => HttpClientFactory.CreateClient("Api");

	private async Task LoadLocationAsync()
	{
		_isLoading = true;
		_errorMessage = null;

		try
		{
			HttpClient client = CreateClient();
			_location = await client.GetFromJsonAsync<LocationResponse>($"/locations/{LocationId}");

			if (_location != null)
			{
				_breadcrumbs =
				[
					new BreadcrumbItem("Home", href: "/"),
					new BreadcrumbItem("Admin", href: "/admin"),
					new BreadcrumbItem("Locations", href: "/admin/locations"),
					new BreadcrumbItem(_location.Name, href: null, disabled: true)
				];

				// Load events
				try
				{
					_events =
						await client.GetFromJsonAsync<LocationEventResponse[]>($"/locations/{LocationId}/events") ?? [];
				}
				catch
				{
					_events = [];
				}

				// Load nearby locations
				try
				{
					_nearbyLocations =
						await client.GetFromJsonAsync<NearbyLocationResponse[]>($"/locations/{LocationId}/nearby") ??
						[];
				}
				catch
				{
					_nearbyLocations = [];
				}
			}
			else
			{
				_errorMessage = "Location not found.";
				_breadcrumbs =
				[
					new BreadcrumbItem("Home", href: "/"),
					new BreadcrumbItem("Admin", href: "/admin"),
					new BreadcrumbItem("Locations", href: "/admin/locations"),
					new BreadcrumbItem("Not Found", href: null, disabled: true)
				];
			}
		}
		catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			_errorMessage = "Location not found.";
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("LocationDetails")
				.LogError(ex, "Failed to load location {LocationId}", LocationId);
			_errorMessage = "Failed to load location: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private void ToggleAddEventForm()
	{
		_showAddEventForm = !_showAddEventForm;
		if (!_showAddEventForm)
		{
			_newEventName = string.Empty;
			_newEventDescription = string.Empty;
		}
	}

	private void CancelAddEvent()
	{
		_showAddEventForm = false;
		_newEventName = string.Empty;
		_newEventDescription = string.Empty;
	}

	private async Task AddEventAsync()
	{
		if (string.IsNullOrWhiteSpace(_newEventName) || string.IsNullOrWhiteSpace(_newEventDescription))
			return;

		_isAddingEvent = true;
		StateHasChanged();

		try
		{
			var request = new
			{
				EventName = _newEventName,
				EventDescription = _newEventDescription
			};

			var response = await CreateClient().PostAsJsonAsync($"/locations/{LocationId}/events", request);

			if (response.IsSuccessStatusCode)
			{
				Snackbar.Add("Event added successfully!", Severity.Success);
				_showAddEventForm = false;
				_newEventName = string.Empty;
				_newEventDescription = string.Empty;

				// Reload events
				try
				{
					_events = await CreateClient()
						.GetFromJsonAsync<LocationEventResponse[]>($"/locations/{LocationId}/events") ?? [];
				}
				catch
				{
					// Ignore reload errors
				}
			}
			else
			{
				var error = await response.Content.ReadAsStringAsync();
				Snackbar.Add($"Failed to add event: {error}", Severity.Error);
			}
		}
		catch (Exception ex)
		{
			Snackbar.Add($"Error: {ex.Message}", Severity.Error);
		}
		finally
		{
			_isAddingEvent = false;
			StateHasChanged();
		}
	}

	private IEnumerable<LocationGraph.NearbyLocationInfo> GetNearbyLocationInfos()
	{
		return _nearbyLocations.Select(n => new LocationGraph.NearbyLocationInfo(
			n.SourceLocationId,
			n.SourceLocationName,
			n.TargetLocationId,
			n.TargetLocationName,
			n.Distance,
			n.TravelNotes));
	}
}
