using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class IndexedDocuments
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    private string[] indexes = [];
    private string? selectedIndex;
    private bool isLoading = false;
    private string? errorMessage;
    private DocumentListResponse? documentList;

    protected override async Task OnInitializedAsync()
    {
        await LoadIndexesAsync();
        await LoadDocumentsAsync();
    }

    private async Task LoadIndexesAsync()
    {
        try
        {
            IndexListResponse? response = await Http.GetFromJsonAsync<IndexListResponse>("/admin/indexes");
            indexes = response?.Indexes ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("IndexedDocuments").LogError(ex, "Failed to load indexes from API");
            errorMessage = "Failed to load indexes: " + ex.Message;
        }
    }

    public async Task LoadDocumentsAsync()
    {
        isLoading = true;
        errorMessage = null;
        documentList = null;

        try
        {
            string url = "/admin/documents";
            if (!string.IsNullOrWhiteSpace(selectedIndex))
            {
                url += $"?index={Uri.EscapeDataString(selectedIndex)}";
            }

            HttpResponseMessage response = await Http.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                documentList = await response.Content.ReadFromJsonAsync<DocumentListResponse>();
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Failed to load documents: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("IndexedDocuments").LogError(ex, "Failed to load documents");
            errorMessage = "Failed to load documents: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}

