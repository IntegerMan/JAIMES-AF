using System.Diagnostics;
using Grpc.Core;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.DocumentEmbedding.Configuration;
using MattEland.Jaimes.Workers.DocumentEmbedding.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using Shouldly;
using CollectionInfo = Qdrant.Client.Grpc.CollectionInfo;
using UpdateResult = Qdrant.Client.Grpc.UpdateResult;
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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyWarningLogged("not found in PostgreSQL when updating Qdrant point ID");
    }

    [Fact]
    public async Task ProcessChunkAsync_WithMissingDocument_LogsWarning()
    {
        using DocumentEmbeddingServiceTestContext context = new();

        // Use a non-existent document ID (but don't insert it into the database)
        int nonExistentDocumentId = 999999;
        
        ChunkReadyForEmbeddingMessage message = new()
        {
            ChunkId = "chunk-1",
            ChunkText = "Test chunk text",
            ChunkIndex = 0,
            DocumentId = nonExistentDocumentId.ToString(),
            FileName = "test.pdf",
            RelativeDirectory = "ruleset-a/source",
            FileSize = 1024,
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        await context.SetupChunkAsync(message.ChunkId, nonExistentDocumentId, message.ChunkText, message.ChunkIndex);
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
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-a"
        };

        int actualDocumentId = await context.SetupDocumentAsync(message.FileName);
        message.DocumentId = actualDocumentId.ToString();
        await context.SetupChunkAsync(message.ChunkId, actualDocumentId, message.ChunkText, message.ChunkIndex);

        float[] expectedEmbedding = new float[] { 0.1f, 0.2f };
        await context.SetupOllamaEmbeddingResponse(expectedEmbedding);

        await context.Service.ProcessChunkAsync(message, CancellationToken.None);

        context.VerifyQdrantPointIdSet(message.ChunkId);
    }

    private sealed class DocumentEmbeddingServiceTestContext : IDisposable
    {
        public Mock<IEmbeddingGenerator<string, Embedding<float>>> EmbeddingGeneratorMock { get; }
        public Mock<IQdrantClient> QdrantClientMock { get; }
        public Mock<ILogger<DocumentEmbeddingService>> LoggerMock { get; }
        public DocumentEmbeddingService Service { get; }
        public JaimesDbContext DbContext { get; }

        private readonly ActivitySource _activitySource;
        private readonly DocumentEmbeddingOptions _options;
        private PointStruct[]? _capturedPoints;
        private VectorParams? _capturedVectorParams;

        public DocumentEmbeddingServiceTestContext()
        {
            EmbeddingGeneratorMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
            QdrantClientMock = new Mock<IQdrantClient>();
            LoggerMock = new Mock<ILogger<DocumentEmbeddingService>>();
            _activitySource = new ActivitySource($"DocumentEmbeddingTests-{Guid.NewGuid()}");
            
            DbContextOptions<JaimesDbContext> dbOptions = new DbContextOptionsBuilder<JaimesDbContext>()
                .UseInMemoryDatabase(databaseName: $"DocumentEmbeddingTests-{Guid.NewGuid()}")
                .Options;
            DbContext = new JaimesDbContext(dbOptions);
            DbContext.Database.EnsureCreated();
            
            _options = new DocumentEmbeddingOptions
            {
                CollectionName = "test-collection"
            };

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

            TestDbContextFactory dbContextFactory = new(dbOptions);

            Service = new DocumentEmbeddingService(
                dbContextFactory,
                EmbeddingGeneratorMock.Object,
                _options,
                QdrantClientMock.Object,
                LoggerMock.Object,
                _activitySource);
        }

        public async Task SetupChunkAsync(string chunkId, int documentId, string chunkText, int chunkIndex)
        {
            DocumentChunk chunk = new()
            {
                ChunkId = chunkId,
                DocumentId = documentId,
                ChunkText = chunkText,
                ChunkIndex = chunkIndex
            };

            DbContext.DocumentChunks.Add(chunk);
            await DbContext.SaveChangesAsync();
        }

        public async Task<int> SetupDocumentAsync(string fileName)
        {
            CrackedDocument document = new()
            {
                FileName = fileName,
                FilePath = $"/{fileName}",
                Content = "Test content",
                IsProcessed = false,
                TotalChunks = 1,
                ProcessedChunkCount = 0,
                RulesetId = "test-ruleset",
                DocumentKind = DocumentKinds.Sourcebook
            };

            DbContext.CrackedDocuments.Add(document);
            await DbContext.SaveChangesAsync();
            return document.Id;
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
            CancellationToken ct = TestContext.Current.CancellationToken;
            // Clear change tracker to ensure we reload from database
            DbContext.ChangeTracker.Clear();
            DocumentChunk? chunk = await DbContext.DocumentChunks
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ChunkId == chunkId, ct);

            chunk.ShouldNotBeNull();
            chunk.QdrantPointId.ShouldNotBeNullOrEmpty();
        }

        public async Task VerifyProcessedChunkCountIncremented(string documentId)
        {
            CancellationToken ct = TestContext.Current.CancellationToken;
            if (!int.TryParse(documentId, out int docId))
            {
                throw new InvalidOperationException($"Invalid document ID: {documentId}");
            }
            // Clear change tracker to ensure we reload from database
            DbContext.ChangeTracker.Clear();
            CrackedDocument? document = await DbContext.CrackedDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == docId, ct);

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
            DbContext.Dispose();
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<JaimesDbContext>
    {
        private readonly DbContextOptions<JaimesDbContext> _options;

        public TestDbContextFactory(DbContextOptions<JaimesDbContext> options)
        {
            _options = options;
        }

        public JaimesDbContext CreateDbContext()
        {
            // Return a new context that shares the same in-memory database
            // This allows the service to dispose it without affecting the test's context
            return new JaimesDbContext(_options);
        }

        public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            // Return a new context that shares the same in-memory database
            // This allows the service to dispose it without affecting the test's context
            return new JaimesDbContext(_options);
        }
    }
}

