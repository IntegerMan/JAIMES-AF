using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Services;

namespace MattEland.Jaimes.Tests.Workers;

public class DocumentChangeDetectorServiceTests
{
    [Fact]
    public async Task ScanAndEnqueueAsync_WhenFileIsNew_PublishesCrackRequest()
    {
        using TempDirectory root = new();
        string subDirectory = Path.Combine(root.Path, "ruleset-a");
        Directory.CreateDirectory(subDirectory);
        string filePath = Path.Combine(subDirectory, "sample.pdf");

        using DocumentChangeDetectorTestContext context = new();
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetSubdirectories(root.Path))
            .Returns(new[] { subDirectory });
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(root.Path, context.Options.SupportedExtensions))
            .Returns(Array.Empty<string>());
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(subDirectory, context.Options.SupportedExtensions))
            .Returns(new[] { filePath });
        context.ChangeTrackerMock
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, TestContext.Current.CancellationToken))
            .ReturnsAsync("hash-new");
        DocumentChangeDetectorService service = context.CreateService();

        DocumentScanSummary summary = await service.ScanAndEnqueueAsync(
            root.Path,
            TestContext.Current.CancellationToken);

        summary.FilesScanned.ShouldBe(1);
        summary.FilesEnqueued.ShouldBe(1);
        summary.FilesUnchanged.ShouldBe(0);
        summary.Errors.ShouldBe(0);

        context.MessagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.Is<CrackDocumentMessage>(message =>
                    message.FilePath == filePath &&
                    message.RelativeDirectory == "ruleset-a" &&
                    message.RulesetId == "ruleset-a"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScanAndEnqueueAsync_WhenFileUnchangedAndCracked_IncrementsUnchangedCount()
    {
        using TempDirectory root = new();
        string subDirectory = Path.Combine(root.Path, "ruleset-b");
        Directory.CreateDirectory(subDirectory);
        string filePath = Path.Combine(subDirectory, "manual.pdf");

        using DocumentChangeDetectorTestContext context = new();
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetSubdirectories(root.Path))
            .Returns(new[] { subDirectory });
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(root.Path, context.Options.SupportedExtensions))
            .Returns(Array.Empty<string>());
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(subDirectory, context.Options.SupportedExtensions))
            .Returns(new[] { filePath });

        context.ChangeTrackerMock
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, TestContext.Current.CancellationToken))
            .ReturnsAsync("hash-static");

        DocumentMetadata metadata = new()
        {
            FilePath = filePath,
            Hash = "hash-static",
            LastScanned = DateTime.UtcNow,
            RulesetId = "ruleset-b",
            DocumentKind = DocumentKinds.Sourcebook
        };
        context.DbContext.DocumentMetadata.Add(metadata);

        CrackedDocument crackedDocument = new()
        {
            FilePath = filePath,
            Content = "already processed",
            FileName = Path.GetFileName(filePath),
            RulesetId = "ruleset-b",
            DocumentKind = DocumentKinds.Sourcebook,
            StoredFileId = 1 // Indicate it already has a file
        };
        context.DbContext.CrackedDocuments.Add(crackedDocument);
        await context.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DocumentChangeDetectorService service = context.CreateService();

        DocumentScanSummary summary = await service.ScanAndEnqueueAsync(
            root.Path,
            TestContext.Current.CancellationToken);

        summary.FilesScanned.ShouldBe(1);
        summary.FilesEnqueued.ShouldBe(0);
        summary.FilesUnchanged.ShouldBe(1);
        summary.Errors.ShouldBe(0);

        context.MessagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.IsAny<CrackDocumentMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ScanAndEnqueueAsync_WhenFileUnchangedButUncracked_RequeuesDocument()
    {
        using TempDirectory root = new();
        string subDirectory = Path.Combine(root.Path, "ruleset-c");
        Directory.CreateDirectory(subDirectory);
        string filePath = Path.Combine(subDirectory, "retry.pdf");

        using DocumentChangeDetectorTestContext context = new();
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetSubdirectories(root.Path))
            .Returns(new[] { subDirectory });
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(root.Path, context.Options.SupportedExtensions))
            .Returns(Array.Empty<string>());
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(subDirectory, context.Options.SupportedExtensions))
            .Returns(new[] { filePath });

        context.ChangeTrackerMock
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, TestContext.Current.CancellationToken))
            .ReturnsAsync("hash-stale");

        DocumentMetadata metadata = new()
        {
            FilePath = filePath,
            Hash = "hash-stale",
            LastScanned = DateTime.UtcNow,
            RulesetId = "ruleset-c",
            DocumentKind = DocumentKinds.Sourcebook
        };
        context.DbContext.DocumentMetadata.Add(metadata);
        await context.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DocumentChangeDetectorService service = context.CreateService();

        DocumentScanSummary summary = await service.ScanAndEnqueueAsync(
            root.Path,
            TestContext.Current.CancellationToken);

        summary.FilesScanned.ShouldBe(1);
        summary.FilesEnqueued.ShouldBe(1);
        summary.FilesUnchanged.ShouldBe(0);
        summary.Errors.ShouldBe(0);

        context.MessagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.Is<CrackDocumentMessage>(message => message.FilePath == filePath),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ScanAndEnqueueAsync_WhenFileCrackedButMissingStoredFile_UploadsDirectly()
    {
        using TempDirectory root = new();
        string subDirectory = Path.Combine(root.Path, "ruleset-d");
        Directory.CreateDirectory(subDirectory);
        string fileName = "upload-me.pdf";
        string filePath = Path.Combine(subDirectory, fileName);
        await File.WriteAllTextAsync(filePath, "PDF CONTENT", TestContext.Current.CancellationToken);

        using DocumentChangeDetectorTestContext context = new();
        context.Options.UploadDocumentsWhenCracking = true;
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetSubdirectories(root.Path))
            .Returns(new[] { subDirectory });
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(root.Path, context.Options.SupportedExtensions))
            .Returns(Array.Empty<string>());
        context.DirectoryScannerMock
            .Setup(scanner => scanner.GetFiles(subDirectory, context.Options.SupportedExtensions))
            .Returns(new[] { filePath });

        context.ChangeTrackerMock
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, TestContext.Current.CancellationToken))
            .ReturnsAsync("hash-ready");

        // Metadata setup
        DocumentMetadata metadata = new()
        {
            FilePath = filePath,
            Hash = "hash-ready",
            LastScanned = DateTime.UtcNow,
            RulesetId = "ruleset-d",
            DocumentKind = DocumentKinds.Sourcebook
        };
        context.DbContext.DocumentMetadata.Add(metadata);

        // Cracked document setup (WITHOUT StoredFileId)
        CrackedDocument crackedDocument = new()
        {
            FilePath = filePath,
            Content = "cracked text",
            FileName = fileName,
            RulesetId = "ruleset-d",
            DocumentKind = DocumentKinds.Sourcebook,
            CrackedAt = DateTime.UtcNow
        };
        context.DbContext.CrackedDocuments.Add(crackedDocument);
        await context.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DocumentChangeDetectorService service = context.CreateService();

        // Act
        DocumentScanSummary summary = await service.ScanAndEnqueueAsync(
            root.Path,
            TestContext.Current.CancellationToken);

        // Assert
        summary.Errors.ShouldBe(0, "Scan should not have errors. If this fails, check the logs for exceptions.");
        summary.FilesScanned.ShouldBe(1);
        summary.FilesEnqueued.ShouldBe(1); // Enqueued = processed/enqueued/uploaded
        summary.FilesUnchanged.ShouldBe(0);

        // Verify StoredFile was created and linked
        var doc = context.DbContext.CrackedDocuments
            .AsNoTracking()
            .Include(d => d.StoredFile)
            .AsEnumerable()
            .FirstOrDefault(d => d.FilePath == filePath);

        doc.ShouldNotBeNull("Document should exist in DB");
        doc.StoredFileId.ShouldNotBeNull("StoredFileId should be set after successful upload.");
        doc.StoredFile.ShouldNotBeNull("StoredFile entity should be linked.");
        doc.StoredFile.FileName.ShouldBe(fileName);
    }

    private sealed class DocumentChangeDetectorTestContext : IDisposable
    {
        public DocumentChangeDetectorOptions Options { get; }
        public Mock<ILogger<DocumentChangeDetectorService>> LoggerMock { get; }
        public Mock<IDirectoryScanner> DirectoryScannerMock { get; }
        public Mock<IChangeTracker> ChangeTrackerMock { get; }
        public JaimesDbContext DbContext { get; }
        public Mock<IMessagePublisher> MessagePublisherMock { get; }

        private readonly ActivitySource _activitySource;
        private readonly DbContextOptions<JaimesDbContext> _dbOptions;

        public DocumentChangeDetectorTestContext()
        {
            Options = new DocumentChangeDetectorOptions
            {
                SupportedExtensions = [".pdf"]
            };

            LoggerMock = new Mock<ILogger<DocumentChangeDetectorService>>();
            DirectoryScannerMock = new Mock<IDirectoryScanner>();
            ChangeTrackerMock = new Mock<IChangeTracker>();
            MessagePublisherMock = new Mock<IMessagePublisher>();

            // Unique ID per test context to avoid collision
            string dbName = $"DocumentChangeDetectorTests-{Guid.NewGuid():N}";
            _dbOptions = new DbContextOptionsBuilder<JaimesDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            DbContext = new JaimesDbContext(_dbOptions);
            DbContext.Database.EnsureDeleted();
            DbContext.Database.EnsureCreated();

            _activitySource = new ActivitySource($"DocumentChangeDetectorTests-{Guid.NewGuid():N}");
        }

        public DocumentChangeDetectorService CreateService()
        {
            TestDbContextFactory dbContextFactory = new(_dbOptions);

            return new DocumentChangeDetectorService(
                LoggerMock.Object,
                DirectoryScannerMock.Object,
                ChangeTrackerMock.Object,
                dbContextFactory,
                MessagePublisherMock.Object,
                _activitySource,
                Options);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            DbContext.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jaimes-detector-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<JaimesDbContext> options)
        : IDbContextFactory<JaimesDbContext>
    {
        public JaimesDbContext CreateDbContext()
        {
            return new JaimesDbContext(options);
        }

        public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new JaimesDbContext(options);
        }
    }
}