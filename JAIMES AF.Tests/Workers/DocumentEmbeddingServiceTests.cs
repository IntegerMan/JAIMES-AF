using System.Diagnostics;
using System.Linq;
using Grpc.Core;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;
using MattEland.Jaimes.Workers.DocumentEmbedding.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Qdrant.Client.Grpc;
using Shouldly;
using UpdateResult = Qdrant.Client.Grpc.UpdateResult;
using CollectionInfo = Qdrant.Client.Grpc.CollectionInfo;
using VectorParams = Qdrant.Client.Grpc.VectorParams;

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyWarningLogged("not found in MongoDB when updating Qdrant point ID");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithMissingDocument_LogsWarning()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        // Use a valid ObjectId format for the documentId (but don't insert it into MongoDB)
        string nonExistentDocumentId = ObjectId.GenerateNewId().ToString();
        
        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = nonExistentDocumentId,
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);
        // Note: Not setting up document here to test missing document scenario

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

        string actualDocumentId = await context.SetupDocumentAsync(message.DocumentId, message.FileName);
        message.DocumentId = actualDocumentId;
        await context.SetupChunkAsync(message.ChunkId, message.DocumentId, message.ChunkText, message.ChunkIndex);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantPointIdSet(message.ChunkId);
    }

    private sealed class DocumentEmbeddingServiceTestContext : IDisposable
    {
        public Mock<IMongoClient> MongoClientMock { get; }
        public Mock<IEmbeddingGenerator<string, Embedding<float>>> EmbeddingGeneratorMock { get; }
        public Mock<IQdrantClient> QdrantClientMock { get; }
        public Mock<ILogger<DocumentEmbeddingService>> LoggerMock { get; }
        public DocumentEmbeddingService Service { get; }
        public IMongoCollection<DocumentChunk> ChunkCollection { get; }
        public IMongoCollection<CrackedDocument> DocumentCollection { get; }

        private readonly ActivitySource _activitySource;
        private readonly MongoTestRunner _mongoRunner;
        private readonly DocumentEmbeddingOptions _options;
        private PointStruct[]? _capturedPoints;
        private VectorParams? _capturedVectorParams;

        public DocumentEmbeddingServiceTestContext()
        {
            MongoClientMock = new Mock<IMongoClient>();
            EmbeddingGeneratorMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
            QdrantClientMock = new Mock<IQdrantClient>();
            LoggerMock = new Mock<ILogger<DocumentEmbeddingService>>();
            _activitySource = new ActivitySource($"DocumentEmbeddingTests-{Guid.NewGuid()}");
            _mongoRunner = new MongoTestRunner();
            _mongoRunner.ResetDatabase();
            _options = new DocumentEmbeddingOptions
            {
                CollectionName = "test-collection"
            };

            IMongoDatabase database = _mongoRunner.Client.GetDatabase("documents");
            ChunkCollection = database.GetCollection<DocumentChunk>("documentChunks");
            DocumentCollection = database.GetCollection<CrackedDocument>("crackedDocuments");

            MongoClientMock
                .Setup(client => client.GetDatabase("documents", It.IsAny<MongoDatabaseSettings>()))
                .Returns(database);

            // Setup UpsertAsync - returns Task<UpdateResult>
            QdrantClientMock
                .Setup(client => client.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<PointStruct[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult())
                .Callback<string, PointStruct[], CancellationToken>((_, points, _) => _capturedPoints = points);

            // Setup CreateCollectionAsync - capture vector params from any call
            QdrantClientMock
                .Setup(client => client.CreateCollectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<VectorParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<string, VectorParams, CancellationToken>((_, vp, _) => _capturedVectorParams = vp);

            // Setup GetCollectionInfoAsync to return null by default (collection doesn't exist)
            QdrantClientMock
                .Setup(client => client.GetCollectionInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((CollectionInfo?)null);

            Service = new DocumentEmbeddingService(
                _mongoRunner.Client,
                EmbeddingGeneratorMock.Object,
                _options,
                QdrantClientMock.Object,
                LoggerMock.Object,
                _activitySource);
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

        public async Task<string> SetupDocumentAsync(string documentId, string fileName)
        {
            // Generate a valid ObjectId from the documentId string for MongoDB compatibility
            // Use a deterministic approach: if documentId is already a valid ObjectId, use it; otherwise generate one
            string objectId;
            if (ObjectId.TryParse(documentId, out ObjectId _))
            {
                objectId = documentId;
            }
            else
            {
                // Generate a deterministic ObjectId from the documentId string using a hash
                // This ensures the same documentId always produces the same ObjectId for test consistency
                byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(documentId));
                // Take first 12 bytes (ObjectId is 12 bytes = 24 hex chars)
                byte[] objectIdBytes = new byte[12];
                Array.Copy(hash, 0, objectIdBytes, 0, 12);
                objectId = new ObjectId(objectIdBytes).ToString();
            }

            CrackedDocument document = new()
            {
                Id = objectId,
                FileName = fileName,
                FilePath = $"/{fileName}",
                Content = "Test content",
                IsProcessed = false,
                TotalChunks = 1,
                ProcessedChunkCount = 0
            };

            await DocumentCollection.InsertOneAsync(document);
            return objectId;
        }

        public Task SetupOllamaEmbeddingResponse(float[] embedding)
        {
            Embedding<float> embeddingObj = new(embedding);
            GeneratedEmbeddings<Embedding<float>> generatedEmbeddings = new([embeddingObj]);
            
            EmbeddingGeneratorMock
                .Setup(generator => generator.GenerateAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<EmbeddingGenerationOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(generatedEmbeddings);

            return Task.CompletedTask;
        }

        public void SetupOllamaError(HttpStatusCode statusCode)
        {
            EmbeddingGeneratorMock
                .Setup(generator => generator.GenerateAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<EmbeddingGenerationOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException($"Error occurred: {statusCode}"));
        }

        public void SetupOllamaEmptyEmbedding()
        {
            GeneratedEmbeddings<Embedding<float>> emptyEmbeddings = new(Array.Empty<Embedding<float>>());
            
            EmbeddingGeneratorMock
                .Setup(generator => generator.GenerateAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<EmbeddingGenerationOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyEmbeddings);
        }

        public void SetupCollectionNotFound()
        {
            // Setup GetCollectionInfoAsync to throw NotFound exception
            QdrantClientMock
                .Setup(client => client.GetCollectionInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RpcException(
                    new Status(StatusCode.NotFound, "Collection not found")));
        }

        public void VerifyQdrantUpsertCalled(string chunkId, float[] embedding, string expectedRulesetId)
        {
            QdrantClientMock.Verify(
                client => client.UpsertAsync(
                    It.Is<string>(name => name == _options.CollectionName),
                    It.IsAny<PointStruct[]>(),
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
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _capturedVectorParams.ShouldNotBeNull();
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
            _mongoRunner.Dispose();
        }
    }

}

