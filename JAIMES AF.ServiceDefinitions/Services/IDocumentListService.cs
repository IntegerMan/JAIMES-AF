using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IDocumentListService
{
    Task<IndexListResponse> ListIndexesAsync(CancellationToken cancellationToken = default);
    Task<DocumentListResponse> ListDocumentsAsync(string? indexName, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}

