using System.Diagnostics;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentChunking.Models;
using MattEland.Jaimes.Workers.DocumentChunking.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class DocumentChunkingServiceTests
{
    [Fact]
    public async Task ProcessDocumentAsync_StoresEmbeddingsAndQueuesMissingChunks()
    {
        using DocumentChunkingServiceTestContext context = new();

        string documentId = ObjectId.GenerateNewId().ToString();

        DocumentReadyForChunkingMessage message = new()
        {
            DocumentId = documentId,
            FileName = "rules.pdf",
            FilePath = "/content/rules.pdf",
            RelativeDirectory = "ruleset-z/source",
            FileSize = 1024,
            PageCount = 12,
            CrackedAt = DateTime.UtcNow,
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-z"
        };

        await context.SetupDocumentContentAsync(message.DocumentId, "Document content", TestContext.Current.CancellationToken);

        List<TextChunk> chunks = new()
        {
            new TextChunk
            {
                Id = "chunk-embedded",
                Text = "With embedding",
                Index = 0,
                SourceDocumentId = message.DocumentId,
                Embedding = new float[] { 0.1f, 0.2f }
            },
            new TextChunk
            {
                Id = "chunk-unembedded",
                Text = "Needs embedding",
                Index = 1,
                SourceDocumentId = message.DocumentId,
                Embedding = null
            }
        };

        context.ChunkingStrategyMock
            .Setup(strategy => strategy.ChunkText("Document content", message.DocumentId))
            .Returns(chunks);

        ChunkReadyForEmbeddingMessage? queuedChunk = null;
        context.MessagePublisherMock
            .Setup(publisher => publisher.PublishAsync(
                It.IsAny<ChunkReadyForEmbeddingMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChunkReadyForEmbeddingMessage, CancellationToken>((chunk, _) => queuedChunk = chunk)
            .Returns(Task.CompletedTask);

        Dictionary<string, string>? storedMetadata = null;
        context.QdrantStoreMock
            .Setup(store => store.StoreEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, float[], Dictionary<string, string>, CancellationToken>((_, _, metadata, _) => storedMetadata = metadata)
            .Returns(Task.CompletedTask);

        await context.Service.ProcessDocumentAsync(
            message,
            TestContext.Current.CancellationToken);

        context.QdrantStoreMock.Verify(
            store => store.StoreEmbeddingAsync(
                "chunk-embedded",
                It.Is<float[]>(embedding => embedding.Length == 2),
                It.Is<Dictionary<string, string>>(metadata => metadata["chunkId"] == "chunk-embedded"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        storedMetadata.ShouldNotBeNull();
        storedMetadata!["documentId"].ShouldBe(message.DocumentId);

        context.MessagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.Is<ChunkReadyForEmbeddingMessage>(chunk =>
                    chunk.ChunkId == "chunk-unembedded" &&
                    chunk.DocumentId == message.DocumentId &&
                    chunk.TotalChunks == chunks.Count),
                It.IsAny<CancellationToken>()),
            Times.Once);
        queuedChunk.ShouldNotBeNull();
        queuedChunk!.RelativeDirectory.ShouldBe(message.RelativeDirectory);
        queuedChunk.DocumentKind.ShouldBe(message.DocumentKind);
        queuedChunk.RulesetId.ShouldBe(message.RulesetId);

        List<DocumentChunk> storedChunks = await context.DocumentChunkCollection
            .Find(Builders<DocumentChunk>.Filter.Eq(chunk => chunk.DocumentId, message.DocumentId))
            .ToListAsync(TestContext.Current.CancellationToken);
        storedChunks.Count.ShouldBe(chunks.Count);
        storedChunks.Any(chunk => chunk.ChunkId == "chunk-embedded").ShouldBeTrue();
        storedChunks.Any(chunk => chunk.ChunkId == "chunk-unembedded").ShouldBeTrue();

        CrackedDocument? updatedDocument = await context.CrackedCollection
            .Find(doc => doc.Id == message.DocumentId)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        updatedDocument.ShouldNotBeNull();
        updatedDocument!.IsProcessed.ShouldBeTrue();
        updatedDocument.TotalChunks.ShouldBe(chunks.Count);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ExtractsPageNumberFromChunkText()
    {
        using DocumentChunkingServiceTestContext context = new();

        string documentId = ObjectId.GenerateNewId().ToString();

        DocumentReadyForChunkingMessage message = new()
        {
            DocumentId = documentId,
            FileName = "rules.pdf",
            FilePath = "/content/rules.pdf",
            RelativeDirectory = "ruleset-z/source",
            FileSize = 1024,
            PageCount = 12,
            CrackedAt = DateTime.UtcNow,
            DocumentKind = DocumentKinds.Sourcebook,
            RulesetId = "ruleset-z"
        };

        await context.SetupDocumentContentAsync(message.DocumentId, "Document content", TestContext.Current.CancellationToken);

        List<TextChunk> chunks = new()
        {
            new TextChunk
            {
                Id = "chunk-with-page",
                Text = "--- Page 5 ---\nSome content from page 5",
                Index = 0,
                SourceDocumentId = message.DocumentId,
                Embedding = null
            },
            new TextChunk
            {
                Id = "chunk-no-page",
                Text = "Content without page marker",
                Index = 1,
                SourceDocumentId = message.DocumentId,
                Embedding = null
            }
        };

        context.ChunkingStrategyMock
            .Setup(strategy => strategy.ChunkText("Document content", message.DocumentId))
            .Returns(chunks);

        ChunkReadyForEmbeddingMessage? queuedChunkWithPage = null;
        ChunkReadyForEmbeddingMessage? queuedChunkNoPage = null;
        context.MessagePublisherMock
            .Setup(publisher => publisher.PublishAsync(
                It.IsAny<ChunkReadyForEmbeddingMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChunkReadyForEmbeddingMessage, CancellationToken>((chunk, _) =>
            {
                if (chunk.ChunkId == "chunk-with-page")
                {
                    queuedChunkWithPage = chunk;
                }
                else if (chunk.ChunkId == "chunk-no-page")
                {
                    queuedChunkNoPage = chunk;
                }
            })
            .Returns(Task.CompletedTask);

        await context.Service.ProcessDocumentAsync(
            message,
            TestContext.Current.CancellationToken);

        queuedChunkWithPage.ShouldNotBeNull();
        queuedChunkWithPage!.PageNumber.ShouldBe(5);

        queuedChunkNoPage.ShouldNotBeNull();
        queuedChunkNoPage!.PageNumber.ShouldBeNull();
    }

    private sealed class DocumentChunkingServiceTestContext : IDisposable
    {
        public Mock<ITextChunkingStrategy> ChunkingStrategyMock { get; }
        public Mock<IQdrantEmbeddingStore> QdrantStoreMock { get; }
        public Mock<IMessagePublisher> MessagePublisherMock { get; }
        public Mock<ILogger<DocumentChunkingService>> LoggerMock { get; }
        public DocumentChunkingService Service { get; }
        public IMongoCollection<CrackedDocument> CrackedCollection => MongoRunner.Client.GetDatabase("documents").GetCollection<CrackedDocument>("crackedDocuments");
        public IMongoCollection<DocumentChunk> DocumentChunkCollection => MongoRunner.Client.GetDatabase("documents").GetCollection<DocumentChunk>("documentChunks");

        private readonly ActivitySource _activitySource;
        private MongoTestRunner MongoRunner { get; }

        public DocumentChunkingServiceTestContext()
        {
            ChunkingStrategyMock = new Mock<ITextChunkingStrategy>();
            QdrantStoreMock = new Mock<IQdrantEmbeddingStore>();
            MessagePublisherMock = new Mock<IMessagePublisher>();
            LoggerMock = new Mock<ILogger<DocumentChunkingService>>();
            MongoRunner = new MongoTestRunner();
            MongoRunner.ResetDatabase();
            _activitySource = new ActivitySource($"DocumentChunkingTests-{Guid.NewGuid()}");

            QdrantStoreMock
                .Setup(store => store.StoreEmbeddingAsync(
                    It.IsAny<string>(),
                    It.IsAny<float[]>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            MessagePublisherMock
                .Setup(publisher => publisher.PublishAsync(
                    It.IsAny<ChunkReadyForEmbeddingMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new DocumentChunkingService(
                MongoRunner.Client,
                ChunkingStrategyMock.Object,
                QdrantStoreMock.Object,
                MessagePublisherMock.Object,
                LoggerMock.Object,
                _activitySource);
        }

        public async Task SetupDocumentContentAsync(string documentId, string content, CancellationToken cancellationToken)
        {
            CrackedDocument document = new()
            {
                Id = documentId,
                Content = content,
                IsProcessed = false
            };

            await CrackedCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            MongoRunner.Dispose();
        }
    }
}
