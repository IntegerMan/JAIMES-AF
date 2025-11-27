using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentChunking.Models;
using MattEland.Jaimes.Workers.DocumentChunking.Services;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class DocumentChunkingServiceTests
{
    [Fact]
    public async Task ProcessDocumentAsync_StoresEmbeddingsAndQueuesMissingChunks()
    {
        using DocumentChunkingServiceTestContext context = new();

        DocumentReadyForChunkingMessage message = new()
        {
            DocumentId = "doc-001",
            FileName = "rules.pdf",
            FilePath = "/content/rules.pdf",
            RelativeDirectory = "ruleset-z/source",
            FileSize = 1024,
            PageCount = 12,
            CrackedAt = DateTime.UtcNow,
            DocumentKind = "Sourcebook",
            RulesetId = "ruleset-z"
        };

        context.SetupDocumentContent(message.DocumentId, "Document content");

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

        context.DocumentChunkCollectionMock.Verify(
            collection => collection.UpdateOneAsync(
                It.IsAny<FilterDefinition<DocumentChunk>>(),
                It.IsAny<UpdateDefinition<DocumentChunk>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(chunks.Count));

        context.CrackedCollectionMock.Verify(
            collection => collection.UpdateOneAsync(
                It.IsAny<FilterDefinition<CrackedDocument>>(),
                It.IsAny<UpdateDefinition<CrackedDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private sealed class DocumentChunkingServiceTestContext : IDisposable
    {
        public Mock<IMongoClient> MongoClientMock { get; }
        public Mock<IMongoDatabase> DatabaseMock { get; }
        public Mock<IMongoCollection<CrackedDocument>> CrackedCollectionMock { get; }
        public Mock<IMongoCollection<DocumentChunk>> DocumentChunkCollectionMock { get; }
        public Mock<IMongoIndexManager<DocumentChunk>> ChunkIndexManagerMock { get; }
        public Mock<ITextChunkingStrategy> ChunkingStrategyMock { get; }
        public Mock<IQdrantEmbeddingStore> QdrantStoreMock { get; }
        public Mock<IMessagePublisher> MessagePublisherMock { get; }
        public Mock<ILogger<DocumentChunkingService>> LoggerMock { get; }
        public DocumentChunkingService Service { get; }

        private readonly ActivitySource _activitySource;

        public DocumentChunkingServiceTestContext()
        {
            MongoClientMock = new Mock<IMongoClient>();
            DatabaseMock = new Mock<IMongoDatabase>();
            CrackedCollectionMock = new Mock<IMongoCollection<CrackedDocument>>();
            DocumentChunkCollectionMock = new Mock<IMongoCollection<DocumentChunk>>();
            ChunkIndexManagerMock = new Mock<IMongoIndexManager<DocumentChunk>>();
            ChunkingStrategyMock = new Mock<ITextChunkingStrategy>();
            QdrantStoreMock = new Mock<IQdrantEmbeddingStore>();
            MessagePublisherMock = new Mock<IMessagePublisher>();
            LoggerMock = new Mock<ILogger<DocumentChunkingService>>();
            _activitySource = new ActivitySource($"DocumentChunkingTests-{Guid.NewGuid()}");

            MongoClientMock
                .Setup(client => client.GetDatabase("documents", null))
                .Returns(DatabaseMock.Object);
            DatabaseMock
                .Setup(db => db.GetCollection<CrackedDocument>("crackedDocuments", null))
                .Returns(CrackedCollectionMock.Object);
            DatabaseMock
                .Setup(db => db.GetCollection<DocumentChunk>("documentChunks", null))
                .Returns(DocumentChunkCollectionMock.Object);

            DocumentChunkCollectionMock
                .SetupGet(collection => collection.Indexes)
                .Returns(ChunkIndexManagerMock.Object);
            ChunkIndexManagerMock
                .Setup(manager => manager.CreateOneAsync(
                    It.IsAny<CreateIndexModel<DocumentChunk>>(),
                    It.IsAny<CreateOneIndexOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("chunk-index");

            DocumentChunkCollectionMock
                .Setup(collection => collection.UpdateOneAsync(
                    It.IsAny<FilterDefinition<DocumentChunk>>(),
                    It.IsAny<UpdateDefinition<DocumentChunk>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAcknowledgedResult());

            CrackedCollectionMock
                .Setup(collection => collection.UpdateOneAsync(
                    It.IsAny<FilterDefinition<CrackedDocument>>(),
                    It.IsAny<UpdateDefinition<CrackedDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAcknowledgedResult());

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
                MongoClientMock.Object,
                ChunkingStrategyMock.Object,
                QdrantStoreMock.Object,
                MessagePublisherMock.Object,
                LoggerMock.Object,
                _activitySource);
        }

        public void SetupDocumentContent(string documentId, string content)
        {
            CrackedDocument document = new()
            {
                Id = documentId,
                Content = content
            };

            CrackedCollectionMock.SetupFindSequence(document);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
        }

        private static UpdateResult CreateAcknowledgedResult()
        {
            Mock<UpdateResult> resultMock = new();
            resultMock.SetupGet(r => r.IsAcknowledged).Returns(true);
            resultMock.SetupGet(r => r.MatchedCount).Returns(1);
            resultMock.SetupGet(r => r.ModifiedCount).Returns(1);
            resultMock.SetupGet(r => r.UpsertedId).Returns(BsonNull.Value);
            return resultMock.Object;
        }
    }
}
