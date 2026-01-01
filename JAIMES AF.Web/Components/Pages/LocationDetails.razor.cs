using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class LocationDetails
{
	[Parameter] public int LocationId { get; set; }
	[Inject] public required IHttpClientFactory HttpClientFactory { get; set; }
	[Inject] public required ILoggerFactory LoggerFactory { get; set; }

	private List<BreadcrumbItem> _breadcrumbs = [];
	private LocationResponse? _location;
	private LocationEventResponse[] _events = [];
	private NearbyLocationResponse[] _nearbyLocations = [];
	private bool _isLoading = true;
	private string? _errorMessage;

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
					_events = await client.GetFromJsonAsync<LocationEventResponse[]>($"/locations/{LocationId}/events") ?? [];
				}
				catch
				{
					_events = [];
				}

				// Load nearby locations
				try
				{
					_nearbyLocations =
						await client.GetFromJsonAsync<NearbyLocationResponse[]>($"/locations/{LocationId}/nearby") ?? [];
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
			LoggerFactory.CreateLogger("LocationDetails").LogError(ex, "Failed to load location {LocationId}", LocationId);
			_errorMessage = "Failed to load location: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}
}
