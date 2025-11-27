using System.Diagnostics;
using MattEland.Jaimes.DocumentProcessing.Services;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tests.TestUtilities;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Configuration;
using MattEland.Jaimes.Workers.DocumentChangeDetector.Services;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using Shouldly;

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
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, It.IsAny<CancellationToken>()))
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
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash-static");

        DocumentMetadata metadata = new()
        {
            FilePath = filePath,
            Hash = "hash-static",
            LastScanned = DateTime.UtcNow
        };
        await context.MetadataCollection.InsertOneAsync(metadata, cancellationToken: TestContext.Current.CancellationToken);

        CrackedDocument crackedDocument = new()
        {
            FilePath = filePath,
            Content = "already processed"
        };
        await context.CrackedCollection.InsertOneAsync(crackedDocument, cancellationToken: TestContext.Current.CancellationToken);

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
            .Setup(tracker => tracker.ComputeFileHashAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash-stale");

        DocumentMetadata metadata = new()
        {
            FilePath = filePath,
            Hash = "hash-stale",
            LastScanned = DateTime.UtcNow
        };
        await context.MetadataCollection.InsertOneAsync(metadata, cancellationToken: TestContext.Current.CancellationToken);

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

    private sealed class DocumentChangeDetectorTestContext : IDisposable
    {
        public DocumentChangeDetectorOptions Options { get; }
        public Mock<ILogger<DocumentChangeDetectorService>> LoggerMock { get; }
        public Mock<IDirectoryScanner> DirectoryScannerMock { get; }
        public Mock<IChangeTracker> ChangeTrackerMock { get; }
        public MongoTestRunner MongoRunner { get; }
        public IMongoCollection<DocumentMetadata> MetadataCollection => MongoRunner.Client.GetDatabase("documents").GetCollection<DocumentMetadata>("documentMetadata");
        public IMongoCollection<CrackedDocument> CrackedCollection => MongoRunner.Client.GetDatabase("documents").GetCollection<CrackedDocument>("crackedDocuments");
        public Mock<IMessagePublisher> MessagePublisherMock { get; }

        private readonly ActivitySource _activitySource;

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
            MongoRunner = new MongoTestRunner();
            MongoRunner.ResetDatabase();
            _activitySource = new ActivitySource($"DocumentChangeDetectorTests-{Guid.NewGuid()}");
        }

        public DocumentChangeDetectorService CreateService()
        {
            return new DocumentChangeDetectorService(
                LoggerMock.Object,
                DirectoryScannerMock.Object,
                ChangeTrackerMock.Object,
                MongoRunner.Client,
                MessagePublisherMock.Object,
                _activitySource,
                Options);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
            MongoRunner.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jaimes-detector-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
