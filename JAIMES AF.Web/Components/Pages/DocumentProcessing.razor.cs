using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class DocumentProcessing
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public IJSRuntime JsRuntime { get; set; } = null!;

    private bool isProcessing;
    private string? errorMessage;
    private string? successMessage;
    private BackfillEmbeddingsResponse? backfillResult;
    private DocumentStatusResponse? documentStatus;
    private bool isLoadingDocuments;
    private string? documentsErrorMessage;
    private string? documentsSuccessMessage;
    private HashSet<string> documentActionsInProgress = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeMermaidDiagramAsync();
            await LoadDocumentsAsync();
        }
    }

    private async Task InitializeMermaidDiagramAsync()
    {
        const string mermaidDefinition = @"
flowchart LR
    A[Document] -->|Scan & Extract<br/>Change Detector Worker +<br/>Document Cracker Worker| B[Text Extraction]
    B -->|Store| C[(MongoDB<br/>Cracked Documents)]
    C -->|Publishes| D[RabbitMQ<br/>DocumentReadyForChunkingMessage]
    D -->|Consumes| E[Chunking & Embedding Worker]
    E -->|Chunks & Generates| F[Chunks with Embeddings]
    F -->|Stores| G[(Qdrant<br/>Vector Database)]
    
    A1@{ shape: braces, label: ""Documents are scanned and cracked before processing"" }
    C1@{ shape: braces, label: ""Cracked documents are stored in MongoDB"" }
    D1@{ shape: braces, label: ""DocumentReadyForChunkingMessage publishes work to RabbitMQ"" }
    E1@{ shape: braces, label: ""Chunking & Embedding worker chunks documents and generates embeddings using SemanticChunker.NET"" }
    G1@{ shape: braces, label: ""Chunks with embeddings are stored directly in the Qdrant vector database"" }
    
    A --- A1
    C --- C1
    D --- D1
    E --- E1
    G --- G1
    
    style A fill:#e1f5ff
    style B fill:#fff4e1
    style C fill:#e8f5e9
    style D fill:#f3e5f5
    style E fill:#fff4e1
    style F fill:#e1f5ff
    style G fill:#e8f5e9
";

        try
        {
            await JsRuntime.InvokeVoidAsync("initializeMermaid", "mermaid-diagram", mermaidDefinition);
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("DocumentProcessing").LogError(ex, "Failed to initialize Mermaid diagram");
        }
    }

    private async Task BackfillEmbeddingsAsync()
    {
        isProcessing = true;
        errorMessage = null;
        successMessage = null;
        backfillResult = null;

        try
        {
            HttpResponseMessage response = await Http.PostAsync("/documents/backfill-embeddings", null);
            
            if (response.IsSuccessStatusCode)
            {
                backfillResult = await response.Content.ReadFromJsonAsync<BackfillEmbeddingsResponse>();
                successMessage = $"Successfully queued {backfillResult?.DocumentsQueued ?? 0} document(s) for embedding processing.";
                
                // Refresh document list after backfill
                await LoadDocumentsAsync();
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Backfill failed: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("DocumentProcessing").LogError(ex, "Failed to backfill embeddings");
            errorMessage = "Failed to backfill embeddings: " + ex.Message;
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task LoadDocumentsAsync(bool clearMessages = true)
    {
        ILogger logger = LoggerFactory.CreateLogger("DocumentProcessing");
        isLoadingDocuments = true;
        if (clearMessages)
        {
            documentsErrorMessage = null;
            documentsSuccessMessage = null;
        }
        StateHasChanged();

        try
        {
            logger.LogInformation("Loading documents from /documents endpoint");
            HttpResponseMessage response = await Http.GetAsync("/documents");
            logger.LogInformation("Received response with status code: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                DocumentStatusResponse? result = await response.Content.ReadFromJsonAsync<DocumentStatusResponse>();
                documentStatus = result;
                int documentCount = result?.Documents.Length ?? 0;
                logger.LogInformation("Successfully loaded {Count} documents", documentCount);
                
                if (documentCount == 0)
                {
                    logger.LogWarning("No documents found in the response");
                }
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                documentsErrorMessage = $"Failed to load documents: {response.StatusCode} - {errorText}";
                logger.LogError("Failed to load documents: {StatusCode} - {ErrorText}", response.StatusCode, errorText);
            }
        }
        catch (Exception ex)
        {
            documentsErrorMessage = $"Failed to load documents: {ex.Message}";
            logger.LogError(ex, "Exception while loading documents: {Message}", ex.Message);
        }
        finally
        {
            isLoadingDocuments = false;
            StateHasChanged();
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private Task RefreshDocumentsAsync()
    {
        return LoadDocumentsAsync();
    }

    private bool IsActionInProgress(string actionKey, DocumentStatusInfo document)
    {
        string key = BuildActionKey(actionKey, document);
        return documentActionsInProgress.Contains(key);
    }

    private void SetActionInProgress(string actionKey, DocumentStatusInfo document, bool inProgress)
    {
        string key = BuildActionKey(actionKey, document);
        if (inProgress)
        {
            documentActionsInProgress.Add(key);
        }
        else
        {
            documentActionsInProgress.Remove(key);
        }
    }

    private static string BuildActionKey(string actionKey, DocumentStatusInfo document)
    {
        return $"{actionKey}:{document.FilePath}";
    }

    private static bool CanQueueEmbeddings(DocumentStatusInfo document)
    {
        return document.IsCracked && !string.IsNullOrWhiteSpace(document.DocumentId);
    }

    private Task RecrackDocumentAsync(DocumentStatusInfo document)
    {
        string? relativeDirectory = string.IsNullOrWhiteSpace(document.RelativeDirectory) ? null : document.RelativeDirectory;
        RecrackDocumentRequest request = new()
        {
            FilePath = document.FilePath,
            RelativeDirectory = relativeDirectory
        };

        return ExecuteDocumentActionAsync(
            "recrack",
            "re-crack",
            document,
            () => Http.PostAsJsonAsync("/documents/recrack", request),
            $"Re-crack requested for {document.FileName}.");
    }

    private Task QueueEmbeddingsForDocumentAsync(DocumentStatusInfo document)
    {
        if (string.IsNullOrWhiteSpace(document.DocumentId))
        {
            documentsErrorMessage = "Cannot queue embeddings because the document is missing its identifier.";
            StateHasChanged();
            return Task.CompletedTask;
        }

        QueueDocumentEmbeddingRequest request = new()
        {
            DocumentId = document.DocumentId
        };

        return ExecuteDocumentActionAsync(
            "queue-embedding",
            "queue embeddings for",
            document,
            () => Http.PostAsJsonAsync("/documents/queue-embedding", request),
            $"Queued embeddings for {document.FileName}.");
    }

    private Task DeleteDocumentAsync(DocumentStatusInfo document)
    {
        DeleteDocumentRequest request = new()
        {
            FilePath = document.FilePath
        };

        return ExecuteDocumentActionAsync(
            "delete",
            "delete",
            document,
            () => Http.PostAsJsonAsync("/documents/delete", request),
            $"Deleted {document.FileName}.");
    }

    private async Task ExecuteDocumentActionAsync(
        string actionKey,
        string actionDescription,
        DocumentStatusInfo document,
        Func<Task<HttpResponseMessage>> action,
        string successFallbackMessage)
    {
        ILogger logger = LoggerFactory.CreateLogger("DocumentProcessing");
        documentsErrorMessage = null;
        documentsSuccessMessage = null;
        SetActionInProgress(actionKey, document, true);
        StateHasChanged();

        try
        {
            HttpResponseMessage response = await action();

            if (response.IsSuccessStatusCode)
            {
                DocumentOperationResponse? result = await response.Content.ReadFromJsonAsync<DocumentOperationResponse>();
                await LoadDocumentsAsync(false);
                documentsSuccessMessage = result?.Message ?? successFallbackMessage;
                logger.LogInformation("{Action} succeeded for {FilePath}", actionDescription, document.FilePath);
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                documentsErrorMessage = $"Failed to {actionDescription} document: {response.StatusCode} - {errorText}";
                logger.LogError(
                    "Failed to {Action} document {FilePath}: {StatusCode} - {ErrorText}",
                    actionDescription,
                    document.FilePath,
                    response.StatusCode,
                    errorText);
            }
        }
        catch (Exception ex)
        {
            documentsErrorMessage = $"Failed to {actionDescription} document: {ex.Message}";
            logger.LogError(ex, "Exception while attempting to {Action} document {FilePath}", actionDescription, document.FilePath);
        }
        finally
        {
            SetActionInProgress(actionKey, document, false);
            StateHasChanged();
        }
    }
}

