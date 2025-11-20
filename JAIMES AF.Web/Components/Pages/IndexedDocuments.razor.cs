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
    private bool isLoadingIndexes = false;
    private string? errorMessage;
    private DocumentListResponse? documentList;
    private int currentPage = 1;
    private int pageSize = 50;

    protected override async Task OnInitializedAsync()
    {
        await LoadIndexesAsync();
        await LoadDocumentsAsync();
    }

    private async Task LoadIndexesAsync()
    {
        isLoadingIndexes = true;
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
        finally
        {
            isLoadingIndexes = false;
            StateHasChanged();
        }
    }

    public async Task LoadDocumentsAsync()
    {
        isLoading = true;
        errorMessage = null;
        documentList = null;
        currentPage = 1; // Reset to first page when loading new index

        try
        {
            string url = $"/admin/documents?page={currentPage}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(selectedIndex))
            {
                url += $"&index={Uri.EscapeDataString(selectedIndex)}";
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

    private async Task LoadPageAsync(int page)
    {
        if (page < 1 || (documentList != null && page > documentList.TotalPages))
        {
            return;
        }

        isLoading = true;
        errorMessage = null;
        currentPage = page;

        try
        {
            string url = $"/admin/documents?page={currentPage}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(selectedIndex))
            {
                url += $"&index={Uri.EscapeDataString(selectedIndex)}";
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

