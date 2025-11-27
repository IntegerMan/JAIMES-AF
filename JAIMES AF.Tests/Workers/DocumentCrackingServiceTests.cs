using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentCracker.Services;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class DocumentCrackingServiceTests
{
    [Fact]
    public async Task ProcessDocumentAsync_WhenFileIsNotPdf_DoesNotTouchDependencies()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ignored-{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, "not a pdf");

        using DocumentCrackingServiceTestContext context = new();

        try
        {
            await context.Service.ProcessDocumentAsync(
                tempFile,
                null,
                "ruleset-x",
                "Sourcebook",
                TestContext.Current.CancellationToken);

            context.MongoClientMock.Verify(
                client => client.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings?>()),
                Times.Never);
            context.MessagePublisherMock.Verify(
                publisher => publisher.PublishAsync(
                    It.IsAny<DocumentReadyForChunkingMessage>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            context.PdfTextExtractorMock.Verify(
                extractor => extractor.ExtractText(It.IsAny<string>()),
                Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessDocumentAsync_WhenPdfIsNew_PublishesChunkingMessage()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"document-{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(filePath, "placeholder"u8.ToArray());

        using DocumentCrackingServiceTestContext context = new();

        context.PdfTextExtractorMock
            .Setup(extractor => extractor.ExtractText(filePath))
            .Returns(("page content", 2));

        ObjectId insertedId = ObjectId.GenerateNewId();
        context.CrackedCollectionMock.SetupFindSequence<CrackedDocument>((CrackedDocument?)null, new CrackedDocument
        {
            Id = insertedId.ToString(),
            FilePath = filePath,
            IsProcessed = false
        });

        context.CrackedCollectionMock
            .Setup(collection => collection.UpdateOneAsync(
                It.IsAny<FilterDefinition<CrackedDocument>>(),
                It.IsAny<UpdateDefinition<CrackedDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAcknowledgedResult(new BsonObjectId(insertedId)));

        DocumentReadyForChunkingMessage? publishedMessage = null;
        context.MessagePublisherMock
            .Setup(publisher => publisher.PublishAsync(
                It.IsAny<DocumentReadyForChunkingMessage>(),
                It.IsAny<CancellationToken>()))
            .Callback<DocumentReadyForChunkingMessage, CancellationToken>((message, _) => publishedMessage = message)
            .Returns(Task.CompletedTask);

        await context.Service.ProcessDocumentAsync(
            filePath,
            "ruleset-y/core",
            "ruleset-y",
            "Sourcebook",
            TestContext.Current.CancellationToken);

        context.PdfTextExtractorMock.Verify(
            extractor => extractor.ExtractText(filePath),
            Times.Once);
        context.MessagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.IsAny<DocumentReadyForChunkingMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.CrackedCollectionMock.Verify(
            collection => collection.UpdateOneAsync(
                It.IsAny<FilterDefinition<CrackedDocument>>(),
                It.IsAny<UpdateDefinition<CrackedDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        publishedMessage.ShouldNotBeNull();
        publishedMessage!.FilePath.ShouldBe(filePath);
        publishedMessage.RelativeDirectory.ShouldBe("ruleset-y/core");
        publishedMessage.DocumentKind.ShouldBe("Sourcebook");
        publishedMessage.RulesetId.ShouldBe("ruleset-y");
        publishedMessage.DocumentId.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed class DocumentCrackingServiceTestContext : IDisposable
    {
        public Mock<ILogger<DocumentCrackingService>> LoggerMock { get; }
        public Mock<IMongoClient> MongoClientMock { get; }
        public Mock<IMongoDatabase> DatabaseMock { get; }
        public Mock<IMongoCollection<CrackedDocument>> CrackedCollectionMock { get; }
        public Mock<IMessagePublisher> MessagePublisherMock { get; }
        public Mock<IPdfTextExtractor> PdfTextExtractorMock { get; }
        public DocumentCrackingService Service { get; }

        private readonly ActivitySource _activitySource;

        public DocumentCrackingServiceTestContext()
        {
            LoggerMock = new Mock<ILogger<DocumentCrackingService>>();
            MongoClientMock = new Mock<IMongoClient>();
            DatabaseMock = new Mock<IMongoDatabase>();
            CrackedCollectionMock = new Mock<IMongoCollection<CrackedDocument>>();
            MessagePublisherMock = new Mock<IMessagePublisher>();
            PdfTextExtractorMock = new Mock<IPdfTextExtractor>();
            _activitySource = new ActivitySource($"DocumentCrackerTests-{Guid.NewGuid()}");

            MongoClientMock
                .Setup(client => client.GetDatabase("documents", null))
                .Returns(DatabaseMock.Object);
            DatabaseMock
                .Setup(db => db.GetCollection<CrackedDocument>("crackedDocuments", null))
                .Returns(CrackedCollectionMock.Object);

            Service = new DocumentCrackingService(
                LoggerMock.Object,
                MongoClientMock.Object,
                MessagePublisherMock.Object,
                _activitySource,
                PdfTextExtractorMock.Object);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
        }

        private static UpdateResult CreateAcknowledgedResult(BsonValue? upsertedId = null)
        {
            Mock<UpdateResult> resultMock = new();
            resultMock.SetupGet(r => r.IsAcknowledged).Returns(true);
            resultMock.SetupGet(r => r.MatchedCount).Returns(1);
            resultMock.SetupGet(r => r.ModifiedCount).Returns(1);
            resultMock.SetupGet(r => r.UpsertedId).Returns(upsertedId);
            return resultMock.Object;
        }
    }
}
