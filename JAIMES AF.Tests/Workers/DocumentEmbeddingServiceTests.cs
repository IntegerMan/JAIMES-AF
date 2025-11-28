using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;
using MattEland.Jaimes.Workers.DocumentEmbedding.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MongoDB.Bson;
using MongoDB.Driver;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class DocumentEmbeddingServiceTests
{
    [Fact]
    public async Task ProcessChunkAsync_GeneratesEmbeddingAndStoresInQdrant()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            FilePath = "/test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            PageCount = 10,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantUpsertCalled(message.ChunkId, expectedEmbedding, "ruleset-a");
        await context.VerifyChunkUpdated(message.ChunkId);
        await context.VerifyProcessedChunkCountIncremented(message.DocumentId);
    }

    [Fact]
    public async Task ProcessChunkAsync_ExtractsRulesetIdFromRelativeDirectory()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "dnd5e/sourcebooks/phb",
            FileSize = 1024,
            DocumentKind = "Sourcebook"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantUpsertCalled(message.ChunkId, expectedEmbedding, "dnd5e");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithEmptyRelativeDirectory_UsesDefaultRulesetId()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "",
            FileSize = 1024,
            DocumentKind = "Sourcebook"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantUpsertCalled(message.ChunkId, expectedEmbedding, "default");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithPageNumber_IncludesPageNumberInMetadata()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            PageNumber = 5,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantMetadataContains(message.ChunkId, "pageNumber", "5");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithOllamaError_ThrowsException()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        context.SetupOllamaError(HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(() =>
            context.Service.ProcessChunkAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessChunkAsync_WithEmptyEmbedding_ThrowsException()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        context.SetupOllamaEmptyEmbedding();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            context.Service.ProcessChunkAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessChunkAsync_WithMissingChunk_LogsWarning()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "non-existent-chunk",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyWarningLogged("not found in MongoDB when updating Qdrant point ID");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithMissingDocument_LogsWarning()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "non-existent-doc",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyWarningLogged("not found when incrementing processed chunk count");
    }

    [Fact]
    public async Task ProcessChunkAsync_GeneratesCorrectQdrantPointId()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = "doc-1",
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        await context.SetupDocumentAsync(message.DocumentId, message.FileName);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantPointIdSet(message.ChunkId);
    }

    private sealed class DocumentEmbeddingServiceTestContext : IDisposable
    {
        public Mock<IMongoClient> MongoClientMock { get; }
        public Mock<HttpMessageHandler> HttpMessageHandlerMock { get; }
        public Mock<QdrantClient> QdrantClientMock { get; }
        public Mock<ILogger<DocumentEmbeddingService>> LoggerMock { get; }
        public DocumentEmbeddingService Service { get; }
        public IMongoCollection<DocumentChunk> ChunkCollection { get; }
        public IMongoCollection<CrackedDocument> DocumentCollection { get; }

        private readonly ActivitySource _activitySource;
        private readonly MongoTestRunner _mongoRunner;
        private readonly HttpClient _httpClient;
        private readonly DocumentEmbeddingOptions _options;
        private PointStruct[]? _capturedPoints;
        private VectorParams? _capturedVectorParams;

        public DocumentEmbeddingServiceTestContext()
        {
            MongoClientMock = new Mock<IMongoClient>();
            HttpMessageHandlerMock = new Mock<HttpMessageHandler>();
            QdrantClientMock = new Mock<QdrantClient>();
            LoggerMock = new Mock<ILogger<DocumentEmbeddingService>>();
            _activitySource = new ActivitySource($"DocumentEmbeddingTests-{Guid.NewGuid()}");
            _mongoRunner = new MongoTestRunner();
            _mongoRunner.ResetDatabase();
            _options = new DocumentEmbeddingOptions
            {
                CollectionName = "test-collection",
                EmbeddingDimensions = 768
            };

            _httpClient = new HttpClient(HttpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:11434")
            };

            IMongoDatabase database = _mongoRunner.Client.GetDatabase("documents");
            ChunkCollection = database.GetCollection<DocumentChunk>("documentChunks");
            DocumentCollection = database.GetCollection<CrackedDocument>("crackedDocuments");

            MongoClientMock
                .Setup(client => client.GetDatabase("documents", It.IsAny<MongoDatabaseSettings>()))
                .Returns(database);

            // Note: GetCollectionInfoAsync is not virtual, so we can't mock it directly.
            // The service will handle the exception when the collection doesn't exist.
            // For tests that need the collection to exist, we'll set it up individually.

            // Setup UpsertAsync - returns Task<UpdateResult>
            QdrantClientMock
                .Setup(client => client.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<PointStruct[]>(),
                    It.IsAny<bool>(),
                    It.IsAny<WriteOrderingType?>(),
                    It.IsAny<ShardKeySelector?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Qdrant.Client.Grpc.UpdateResult())
                .Callback<string, PointStruct[], bool, WriteOrderingType?, ShardKeySelector?, CancellationToken>((_, points, _, _, _, _) => _capturedPoints = points);

            // Setup CreateCollectionAsync - capture vector params from any call
            // Signature: CreateCollectionAsync(string, VectorParams, uint timeout, CancellationToken)
            QdrantClientMock
                .Setup(client => client.CreateCollectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<VectorParams>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<bool>(),
                    It.IsAny<HnswConfigDiff?>(),
                    It.IsAny<OptimizersConfigDiff?>(),
                    It.IsAny<WalConfigDiff?>(),
                    It.IsAny<QuantizationConfig?>(),
                    It.IsAny<string?>(),
                    It.IsAny<ShardingMethod?>(),
                    It.IsAny<SparseVectorConfig?>(),
                    It.IsAny<StrictModeConfig?>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<string, VectorParams, uint, uint, uint, bool, HnswConfigDiff?, OptimizersConfigDiff?, WalConfigDiff?, QuantizationConfig?, string?, ShardingMethod?, SparseVectorConfig?, StrictModeConfig?, TimeSpan?, CancellationToken>((_, vp, _, _, _, _, _, _, _, _, _, _, _, _, _, _) => _capturedVectorParams = vp);

            Service = new DocumentEmbeddingService(
                _mongoRunner.Client,
                _httpClient,
                _options,
                QdrantClientMock.Object,
                LoggerMock.Object,
                _activitySource,
                "http://localhost:11434",
                "nomic-embed-text");
        }

        public async Task SetupChunkAsync(string chunkId, string documentId, string chunkText, int chunkIndex)
        {
            DocumentChunk chunk = new()
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ChunkId = chunkId,
                DocumentId = documentId,
                ChunkText = chunkText,
                ChunkIndex = chunkIndex
            };

            await ChunkCollection.InsertOneAsync(chunk);
        }

        public async Task SetupDocumentAsync(string documentId, string fileName)
        {
            CrackedDocument document = new()
            {
                Id = documentId,
                FileName = fileName,
                FilePath = $"/{fileName}",
                Content = "Test content",
                IsProcessed = false,
                TotalChunks = 1,
                ProcessedChunkCount = 0
            };

            await DocumentCollection.InsertOneAsync(document);
        }

        public Task SetupOllamaEmbeddingResponse(float[] embedding)
        {
            OllamaEmbeddingResponse response = new()
            {
                Embedding = embedding
            };

            HttpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri != null &&
                        req.RequestUri.ToString().Contains("/api/embeddings")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(response)
                });

            return Task.CompletedTask;
        }

        public void SetupOllamaError(HttpStatusCode statusCode)
        {
            HttpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent("Error occurred")
                });
        }

        public void SetupOllamaEmptyEmbedding()
        {
            OllamaEmbeddingResponse response = new()
            {
                Embedding = Array.Empty<float>()
            };

            HttpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(response)
                });
        }

        public void SetupCollectionNotFound()
        {
            // Note: GetCollectionInfoAsync is not virtual, so we can't mock it directly.
            // The service will call the real method which will fail on the mock.
            // Since the service catches NotFound exceptions and creates the collection,
            // we don't need to set up this method - the service will handle the failure.
        }

        public void VerifyQdrantUpsertCalled(string chunkId, float[] embedding, string expectedRulesetId)
        {
            QdrantClientMock.Verify(
                client => client.UpsertAsync(
                    It.Is<string>(name => name == _options.CollectionName),
                    It.IsAny<PointStruct[]>(),
                    It.IsAny<bool>(),
                    It.IsAny<WriteOrderingType?>(),
                    It.IsAny<ShardKeySelector?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _capturedPoints.ShouldNotBeNull();
            _capturedPoints!.Length.ShouldBe(1);
            _capturedPoints[0].Vectors.ShouldNotBeNull();
            _capturedPoints[0].Payload.ContainsKey("chunkId").ShouldBeTrue();
            _capturedPoints[0].Payload["chunkId"].StringValue.ShouldBe(chunkId);
            _capturedPoints[0].Payload.ContainsKey("rulesetId").ShouldBeTrue();
            _capturedPoints[0].Payload["rulesetId"].StringValue.ShouldBe(expectedRulesetId);
        }

        public void VerifyQdrantMetadataContains(string chunkId, string key, string value)
        {
            QdrantClientMock.Verify(
                client => client.UpsertAsync(
                    It.Is<string>(name => name == _options.CollectionName),
                    It.IsAny<PointStruct[]>(),
                    It.IsAny<bool>(),
                    It.IsAny<WriteOrderingType?>(),
                    It.IsAny<ShardKeySelector?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _capturedPoints.ShouldNotBeNull();
            _capturedPoints!.Length.ShouldBe(1);
            _capturedPoints[0].Payload.ContainsKey(key).ShouldBeTrue();
            _capturedPoints[0].Payload[key].StringValue.ShouldBe(value);
        }

        public void VerifyCollectionCreated()
        {
            QdrantClientMock.Verify(
                client => client.CreateCollectionAsync(
                    It.Is<string>(name => name == _options.CollectionName),
                    It.IsAny<VectorParams>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<uint>(),
                    It.IsAny<bool>(),
                    It.IsAny<HnswConfigDiff?>(),
                    It.IsAny<OptimizersConfigDiff?>(),
                    It.IsAny<WalConfigDiff?>(),
                    It.IsAny<QuantizationConfig?>(),
                    It.IsAny<string?>(),
                    It.IsAny<ShardingMethod?>(),
                    It.IsAny<SparseVectorConfig?>(),
                    It.IsAny<StrictModeConfig?>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _capturedVectorParams.ShouldNotBeNull();
            _capturedVectorParams!.Size.ShouldBe((ulong)_options.EmbeddingDimensions);
            _capturedVectorParams.Distance.ShouldBe(Distance.Cosine);
        }

        public async Task VerifyChunkUpdated(string chunkId)
        {
            DocumentChunk? chunk = await ChunkCollection
                .Find(c => c.ChunkId == chunkId)
                .FirstOrDefaultAsync();

            chunk.ShouldNotBeNull();
            chunk.QdrantPointId.ShouldNotBeNullOrEmpty();
        }

        public async Task VerifyProcessedChunkCountIncremented(string documentId)
        {
            CrackedDocument? document = await DocumentCollection
                .Find(d => d.Id == documentId)
                .FirstOrDefaultAsync();

            document.ShouldNotBeNull();
            document.ProcessedChunkCount.ShouldBeGreaterThan(0);
        }

        public void VerifyQdrantPointIdSet(string chunkId)
        {
            QdrantClientMock.Verify(
                client => client.UpsertAsync(
                    It.Is<string>(name => name == _options.CollectionName),
                    It.IsAny<PointStruct[]>(),
                    It.IsAny<bool>(),
                    It.IsAny<WriteOrderingType?>(),
                    It.IsAny<ShardKeySelector?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _capturedPoints.ShouldNotBeNull();
            _capturedPoints!.Length.ShouldBe(1);
            _capturedPoints[0].Id.ShouldNotBe(default(PointId));
        }

        public void VerifyWarningLogged(string message)
        {
            LoggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            _httpClient.Dispose();
            _mongoRunner.Dispose();
        }
    }

    private record OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; init; }
    }
}

