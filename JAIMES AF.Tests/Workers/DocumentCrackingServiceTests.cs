using System.Diagnostics;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentCrackerWorker.Services;
using Microsoft.Extensions.Logging;
using Moq;
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

        CrackedDocument? storedDocument = await context.CrackedCollection
            .Find(d => d.FilePath == filePath)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        storedDocument.ShouldNotBeNull();
        storedDocument!.Content.ShouldBe("page content");
        storedDocument.IsProcessed.ShouldBeFalse();
        storedDocument.RulesetId.ShouldBe("ruleset-y");
        storedDocument.DocumentKind.ShouldBe("Sourcebook");

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
        public Mock<IMessagePublisher> MessagePublisherMock { get; }
        public Mock<IPdfTextExtractor> PdfTextExtractorMock { get; }
        public DocumentCrackingService Service { get; }
        public IMongoCollection<CrackedDocument> CrackedCollection => MongoRunner.Client.GetDatabase("documents").GetCollection<CrackedDocument>("crackedDocuments");
        private MongoTestRunner MongoRunner { get; }

        private readonly ActivitySource _activitySource;

        public DocumentCrackingServiceTestContext()
        {
            LoggerMock = new Mock<ILogger<DocumentCrackingService>>();
            MessagePublisherMock = new Mock<IMessagePublisher>();
            PdfTextExtractorMock = new Mock<IPdfTextExtractor>();
            MongoRunner = new MongoTestRunner();
            MongoRunner.ResetDatabase();
            _activitySource = new ActivitySource($"DocumentCrackerTests-{Guid.NewGuid()}");

            Service = new DocumentCrackingService(
                LoggerMock.Object,
                MongoRunner.Client,
                MessagePublisherMock.Object,
                _activitySource,
                PdfTextExtractorMock.Object);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            MongoRunner.Dispose();
        }

    }
}
