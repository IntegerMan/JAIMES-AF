using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditLocation
{
	[Parameter] public int LocationId { get; set; }
	[Inject] public required IHttpClientFactory HttpClientFactory { get; set; }
	[Inject] public required ILoggerFactory LoggerFactory { get; set; }
	[Inject] public required NavigationManager Navigation { get; set; }
	[Inject] public required ISnackbar Snackbar { get; set; }

	private List<BreadcrumbItem> _breadcrumbs = [];
	private LocationResponse? _location;
	private MudForm? _form;
	private bool _isValid;
	private bool _isLoading = true;
	private bool _isSaving;
	private string? _errorMessage;

	// Form fields
	private string _name = string.Empty;
	private string _description = string.Empty;
	private string? _appearance;
	private string? _storytellerNotes;

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
			_location = await CreateClient().GetFromJsonAsync<LocationResponse>($"/locations/{LocationId}");

			if (_location != null)
			{
				_name = _location.Name;
				_description = _location.Description;
				_appearance = _location.Appearance;
				_storytellerNotes = _location.StorytellerNotes;

				_breadcrumbs =
				[
					new BreadcrumbItem("Home", href: "/"),
					new BreadcrumbItem("Admin", href: "/admin"),
					new BreadcrumbItem("Locations", href: "/admin/locations"),
					new BreadcrumbItem(_location.Name, href: $"/admin/locations/{LocationId}"),
					new BreadcrumbItem("Edit", href: null, disabled: true)
				];
			}
			else
			{
				_errorMessage = "Location not found.";
			}
		}
		catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			_errorMessage = "Location not found.";
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("EditLocation").LogError(ex, "Failed to load location {LocationId}", LocationId);
			_errorMessage = "Failed to load location: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private async Task SaveAsync()
	{
		if (_form == null) return;

		await _form.Validate();
		if (!_isValid) return;

		_isSaving = true;
		StateHasChanged();

		try
		{
			var updateRequest = new
			{
				Description = _description,
				Appearance = _appearance,
				StorytellerNotes = _storytellerNotes
			};

			var response = await CreateClient().PutAsJsonAsync($"/locations/{LocationId}", updateRequest);

			if (response.IsSuccessStatusCode)
			{
				Snackbar.Add("Location saved successfully!", Severity.Success);
				Navigation.NavigateTo($"/admin/locations/{LocationId}");
			}
			else
			{
				var error = await response.Content.ReadAsStringAsync();
				Snackbar.Add($"Failed to save: {error}", Severity.Error);
			}
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("EditLocation").LogError(ex, "Failed to save location {LocationId}", LocationId);
			Snackbar.Add("Failed to save location: " + ex.Message, Severity.Error);
		}
		finally
		{
			_isSaving = false;
			StateHasChanged();
		}
	}
}
