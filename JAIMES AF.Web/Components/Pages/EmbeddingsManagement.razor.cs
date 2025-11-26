using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EmbeddingsManagement
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public IDialogService DialogService { get; set; } = null!;

    private EmbeddingListItem[]? embeddings;
    private bool isLoadingEmbeddings;
    private bool isDeletingAll;
    private string? errorMessage;
    private string? successMessage;
    private HashSet<string> deletingEmbeddings = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadEmbeddingsAsync();
        }
    }

    private async Task LoadEmbeddingsAsync(bool clearMessages = true)
    {
        ILogger logger = LoggerFactory.CreateLogger("EmbeddingsManagement");
        isLoadingEmbeddings = true;
        if (clearMessages)
        {
            errorMessage = null;
            successMessage = null;
        }
        StateHasChanged();

        try
        {
            logger.LogInformation("Loading embeddings from /embeddings endpoint");
            HttpResponseMessage response = await Http.GetAsync("/embeddings");
            logger.LogInformation("Received response with status code: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                EmbeddingListResponse? result = await response.Content.ReadFromJsonAsync<EmbeddingListResponse>();
                embeddings = result?.Embeddings ?? [];
                int embeddingCount = embeddings.Length;
                logger.LogInformation("Successfully loaded {Count} embeddings", embeddingCount);

                if (embeddingCount == 0)
                {
                    logger.LogWarning("No embeddings found in the response");
                }
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Failed to load embeddings: {response.StatusCode} - {errorText}";
                logger.LogError("Failed to load embeddings: {StatusCode} - {ErrorText}", response.StatusCode, errorText);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to load embeddings: {ex.Message}";
            logger.LogError(ex, "Exception while loading embeddings: {Message}", ex.Message);
        }
        finally
        {
            isLoadingEmbeddings = false;
            StateHasChanged();
        }
    }

    private Task RefreshEmbeddingsAsync()
    {
        return LoadEmbeddingsAsync();
    }

    private bool IsDeleting(EmbeddingListItem embedding)
    {
        return deletingEmbeddings.Contains(embedding.EmbeddingId);
    }

    private void SetDeleting(EmbeddingListItem embedding, bool deleting)
    {
        if (deleting)
        {
            deletingEmbeddings.Add(embedding.EmbeddingId);
        }
        else
        {
            deletingEmbeddings.Remove(embedding.EmbeddingId);
        }
    }

    private async Task DeleteEmbeddingAsync(EmbeddingListItem embedding)
    {
        ILogger logger = LoggerFactory.CreateLogger("EmbeddingsManagement");
        errorMessage = null;
        successMessage = null;
        SetDeleting(embedding, true);
        StateHasChanged();

        try
        {
            HttpResponseMessage response = await Http.DeleteAsync($"/embeddings/{Uri.EscapeDataString(embedding.EmbeddingId)}");

            if (response.IsSuccessStatusCode)
            {
                DocumentOperationResponse? result = await response.Content.ReadFromJsonAsync<DocumentOperationResponse>();
                await LoadEmbeddingsAsync(false);
                successMessage = result?.Message ?? $"Successfully deleted embedding {embedding.EmbeddingId}";
                logger.LogInformation("Successfully deleted embedding {EmbeddingId}", embedding.EmbeddingId);
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Failed to delete embedding: {response.StatusCode} - {errorText}";
                logger.LogError("Failed to delete embedding {EmbeddingId}: {StatusCode} - {ErrorText}",
                    embedding.EmbeddingId, response.StatusCode, errorText);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to delete embedding: {ex.Message}";
            logger.LogError(ex, "Exception while deleting embedding {EmbeddingId}", embedding.EmbeddingId);
        }
        finally
        {
            SetDeleting(embedding, false);
            StateHasChanged();
        }
    }

    private async Task ShowDeleteAllDialog()
    {
        int embeddingCount = embeddings?.Length ?? 0;
        bool? result = await DialogService.ShowMessageBox(
            "Delete All Embeddings?",
            $"Are you sure you want to delete all {embeddingCount} embeddings? This action cannot be undone.",
            yesText: "Delete All",
            cancelText: "Cancel");

        if (result == true)
        {
            await DeleteAllEmbeddingsAsync();
        }
    }

    private async Task DeleteAllEmbeddingsAsync()
    {
        ILogger logger = LoggerFactory.CreateLogger("EmbeddingsManagement");
        errorMessage = null;
        successMessage = null;
        isDeletingAll = true;
        StateHasChanged();

        try
        {
            HttpResponseMessage response = await Http.DeleteAsync("/embeddings");

            if (response.IsSuccessStatusCode)
            {
                DocumentOperationResponse? result = await response.Content.ReadFromJsonAsync<DocumentOperationResponse>();
                await LoadEmbeddingsAsync(false);
                successMessage = result?.Message ?? "Successfully deleted all embeddings";
                logger.LogInformation("Successfully deleted all embeddings");
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Failed to delete all embeddings: {response.StatusCode} - {errorText}";
                logger.LogError("Failed to delete all embeddings: {StatusCode} - {ErrorText}",
                    response.StatusCode, errorText);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to delete all embeddings: {ex.Message}";
            logger.LogError(ex, "Exception while deleting all embeddings: {Message}", ex.Message);
        }
        finally
        {
            isDeletingAll = false;
            StateHasChanged();
        }
    }
}



