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
                DocumentKinds.Sourcebook,
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

        CrackedDocument? storedDocument = await context.DbContext.CrackedDocuments
            .FirstOrDefaultAsync(d => d.FilePath == filePath, TestContext.Current.CancellationToken);
        storedDocument.ShouldNotBeNull();
        storedDocument!.Content.ShouldBe("page content");
        storedDocument.IsProcessed.ShouldBeFalse();
        storedDocument.RulesetId.ShouldBe("ruleset-y");
        storedDocument.DocumentKind.ShouldBe(DocumentKinds.Sourcebook);

        publishedMessage.ShouldNotBeNull();
        publishedMessage!.FilePath.ShouldBe(filePath);
        publishedMessage.RelativeDirectory.ShouldBe("ruleset-y/core");
        publishedMessage.DocumentKind.ShouldBe(DocumentKinds.Sourcebook);
        publishedMessage.RulesetId.ShouldBe("ruleset-y");
        publishedMessage.DocumentId.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed class DocumentCrackingServiceTestContext : IDisposable
    {
        public Mock<ILogger<DocumentCrackingService>> LoggerMock { get; }
        public Mock<IMessagePublisher> MessagePublisherMock { get; }
        public Mock<IPdfTextExtractor> PdfTextExtractorMock { get; }
        public DocumentCrackingService Service { get; }
        public JaimesDbContext DbContext { get; }

        private readonly ActivitySource _activitySource;

        public DocumentCrackingServiceTestContext()
        {
            LoggerMock = new Mock<ILogger<DocumentCrackingService>>();
            MessagePublisherMock = new Mock<IMessagePublisher>();
            PdfTextExtractorMock = new Mock<IPdfTextExtractor>();

            DbContextOptions<JaimesDbContext> dbOptions = new DbContextOptionsBuilder<JaimesDbContext>()
                .UseInMemoryDatabase($"DocumentCrackerTests-{Guid.NewGuid()}")
                .Options;
            DbContext = new JaimesDbContext(dbOptions);
            DbContext.Database.EnsureCreated();

            _activitySource = new ActivitySource($"DocumentCrackerTests-{Guid.NewGuid()}");

            TestDbContextFactory dbContextFactory = new(dbOptions);

            // Create options with default values (UploadDocumentsToDatabase = true)
            var crackingOptions = Microsoft.Extensions.Options.Options.Create(
                new MattEland.Jaimes.ServiceDefinitions.Configuration.DocumentCrackingOptions());

            Service = new DocumentCrackingService(
                LoggerMock.Object,
                dbContextFactory,
                MessagePublisherMock.Object,
                _activitySource,
                PdfTextExtractorMock.Object,
                crackingOptions);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            DbContext.Dispose();
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<JaimesDbContext> options)
        : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext()
        {
            // Return a new context that shares the same in-memory database
            // This allows the service to dispose it without affecting the test's context
            return new JaimesDbContext(options);
        }

        public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            // Return a new context that shares the same in-memory database
            // This allows the service to dispose it without affecting the test's context
            return new JaimesDbContext(options);
        }
    }
}