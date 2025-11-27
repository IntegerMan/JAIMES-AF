using FastEndpoints;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListDocumentsEndpoint : Ep.NoReq.Res<DocumentStatusResponse>
{
    public required IMongoClient MongoClient { get; set; }

    public override void Configure()
    {
        Get("/documents");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentStatusResponse>()
            .WithTags("Documents")
            .WithSummary("List all detected documents with their processing status"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Listing all documents with processing status");

        // Get database and collections
        IMongoDatabase mongoDatabase = MongoClient.GetDatabase("documents");
        Logger.LogDebug("Connected to MongoDB database: documents");

        IMongoCollection<DocumentMetadata> metadataCollection = mongoDatabase.GetCollection<DocumentMetadata>("documentMetadata");
        IMongoCollection<CrackedDocument> crackedCollection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");
        Logger.LogDebug("Retrieved collections: documentMetadata, crackedDocuments");

        // Get all detected documents
        List<DocumentMetadata> allMetadata = await metadataCollection.Find(_ => true).ToListAsync(ct);
        Logger.LogInformation("Found {Count} detected documents in documentMetadata collection", allMetadata.Count);

        // Get all cracked documents for quick lookup
        List<CrackedDocument> allCracked = await crackedCollection.Find(_ => true).ToListAsync(ct);
        Logger.LogInformation("Found {Count} cracked documents in crackedDocuments collection", allCracked.Count);

        Dictionary<string, CrackedDocument> crackedByPath = allCracked
            .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
            .ToDictionary(d => d.FilePath, d => d, StringComparer.OrdinalIgnoreCase);

        // Build response
        List<DocumentStatusInfo> documents = new();
        foreach (DocumentMetadata metadata in allMetadata)
        {
            bool hasCracked = crackedByPath.TryGetValue(metadata.FilePath, out CrackedDocument? cracked);
            bool isCracked = hasCracked && !string.IsNullOrWhiteSpace(cracked?.Content);
            bool hasEmbeddings = hasCracked && cracked?.IsProcessed == true;

            documents.Add(new DocumentStatusInfo
            {
                DocumentId = hasCracked ? cracked!.Id : null,
                FilePath = metadata.FilePath,
                FileName = Path.GetFileName(metadata.FilePath),
                RelativeDirectory = hasCracked ? cracked!.RelativeDirectory : string.Empty,
                IsCracked = isCracked,
                HasEmbeddings = hasEmbeddings,
                CrackedAt = hasCracked ? cracked!.CrackedAt : null,
                FileSize = hasCracked ? cracked!.FileSize : null,
                PageCount = hasCracked ? cracked!.PageCount : null,
                DocumentType = hasCracked ? cracked!.DocumentType : null,
                RulesetId = hasCracked ? cracked!.RulesetId : null
            });
        }

        // Sort by file path
        documents = documents.OrderBy(d => d.FilePath).ToList();

        Logger.LogInformation("Returning {Count} documents with status information", documents.Count);

        await Send.OkAsync(new DocumentStatusResponse
        {
            Documents = documents.ToArray()
        }, cancellation: ct);
    }
}

