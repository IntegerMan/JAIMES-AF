using MattEland.Jaimes.Indexer.Configuration;
using MattEland.Jaimes.Indexer.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace MattEland.Jaimes.Tests.Services;

public class IndexingOrchestratorTests
{
    private readonly Mock<ILogger<IndexingOrchestrator>> _loggerMock;
    private readonly Mock<IDirectoryScanner> _directoryScannerMock;
    private readonly Mock<IChangeTracker> _changeTrackerMock;
    private readonly Mock<IDocumentIndexer> _documentIndexerMock;
    private readonly IndexerOptions _options;
    private readonly IndexingOrchestrator _orchestrator;

    public IndexingOrchestratorTests()
    {
        _loggerMock = new Mock<ILogger<IndexingOrchestrator>>();
        _directoryScannerMock = new Mock<IDirectoryScanner>();
        _changeTrackerMock = new Mock<IChangeTracker>();
        _documentIndexerMock = new Mock<IDocumentIndexer>();

        _options = new IndexerOptions
        {
            SourceDirectory = "C:\\Test\\Documents",
            VectorDbConnectionString = "Data Source=test.db",
            OpenAiEndpoint = "https://test.openai.azure.com/",
            OpenAiApiKey = "test-key",
            OpenAiDeployment = "test-deployment",
            SupportedExtensions = new List<string> { ".txt", ".md", ".pdf" }
        };

        _orchestrator = new IndexingOrchestrator(
            _loggerMock.Object,
            _directoryScannerMock.Object,
            _changeTrackerMock.Object,
            _documentIndexerMock.Object,
            _options);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenNoSubdirectories_ProcessesRootDirectoryOnly()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string rootIndex = "index-root";

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { Path.Combine(rootDir, "file1.txt") });

        _changeTrackerMock
            .Setup(t => t.ComputeFileHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash1");

        _documentIndexerMock
            .Setup(i => i.DocumentExistsAsync(It.IsAny<string>(), rootIndex, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(It.IsAny<string>(), rootIndex, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        summary.TotalProcessed.ShouldBe(1);
        summary.TotalAdded.ShouldBe(1);
        summary.TotalUpdated.ShouldBe(0);
        summary.TotalErrors.ShouldBe(0);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(5, 3, 2)]
    public async Task ProcessAllDirectoriesAsync_WithMultipleSubdirectories_ProcessesAll(
        int subdirectoryCount, int newFiles, int existingFiles)
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        List<string> subdirs = Enumerable.Range(1, subdirectoryCount)
            .Select(i => Path.Combine(rootDir, $"subdir{i}"))
            .ToList();

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(subdirs);

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(Enumerable.Empty<string>());

        int fileIndex = 0;
        foreach (string subdir in subdirs)
        {
            string indexName = $"index-subdir{fileIndex + 1}";
            List<string> files = new();
            
            // Add new files
            for (int i = 0; i < newFiles; i++)
            {
                files.Add(Path.Combine(subdir, $"file{fileIndex++}.txt"));
            }
            
            // Add existing files
            for (int i = 0; i < existingFiles; i++)
            {
                files.Add(Path.Combine(subdir, $"existing{fileIndex++}.txt"));
            }

            _directoryScannerMock
                .Setup(s => s.GetFiles(subdir, _options.SupportedExtensions))
                .Returns(files);

            foreach (string file in files)
            {
                _changeTrackerMock
                    .Setup(t => t.ComputeFileHashAsync(file, It.IsAny<CancellationToken>()))
                    .ReturnsAsync($"hash-{file}");

                bool isExisting = file.Contains("existing");
                _documentIndexerMock
                    .Setup(i => i.DocumentExistsAsync(file, indexName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(isExisting);

                _documentIndexerMock
                    .Setup(i => i.IndexDocumentAsync(file, indexName, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }
        }

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        int totalFiles = subdirectoryCount * (newFiles + existingFiles);
        summary.TotalProcessed.ShouldBe(totalFiles);
        summary.TotalAdded.ShouldBe(subdirectoryCount * newFiles);
        summary.TotalUpdated.ShouldBe(subdirectoryCount * existingFiles);
        summary.TotalErrors.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenFileIndexingFails_IncrementsErrorCount()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath = Path.Combine(rootDir, "file.txt");
        string rootIndex = "index-root";

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { filePath });

        _changeTrackerMock
            .Setup(t => t.ComputeFileHashAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash1");

        _documentIndexerMock
            .Setup(i => i.DocumentExistsAsync(filePath, rootIndex, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, rootIndex, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Indexing fails

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        summary.TotalProcessed.ShouldBe(1);
        summary.TotalErrors.ShouldBe(1);
        summary.TotalAdded.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenExceptionThrown_LogsErrorAndContinues()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath1 = Path.Combine(rootDir, "file1.txt");
        string filePath2 = Path.Combine(rootDir, "file2.txt");
        string rootIndex = "index-root";

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { filePath1, filePath2 });

        _changeTrackerMock
            .Setup(t => t.ComputeFileHashAsync(filePath1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("File access error"));

        _changeTrackerMock
            .Setup(t => t.ComputeFileHashAsync(filePath2, It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash2");

        _documentIndexerMock
            .Setup(i => i.DocumentExistsAsync(filePath2, rootIndex, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath2, rootIndex, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        summary.TotalProcessed.ShouldBe(2);
        summary.TotalErrors.ShouldBe(1);
        summary.TotalAdded.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenCancellationRequested_StopsProcessing()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string subdir = Path.Combine(rootDir, "subdir1");

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(new[] { subdir });

        _directoryScannerMock
            .Setup(s => s.GetFiles(subdir, _options.SupportedExtensions))
            .Returns(new[] { Path.Combine(subdir, "file1.txt") });

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(cts.Token);

        // Assert
        summary.TotalProcessed.ShouldBe(0);
    }

    [Theory]
    [InlineData("C:\\Test\\RulesetA", "index-ruleseta")]
    [InlineData("C:\\Test\\RulesetB", "index-rulesetb")]
    [InlineData("C:\\Test\\My Rules", "index-my-rules")]
    [InlineData("C:\\Test\\", "index-root")]
    [InlineData("/home/test/rulesetA", "index-ruleseta")]
    public async Task GetIndexName_GeneratesCorrectIndexName(string directoryPath, string expectedIndexName)
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath = Path.Combine(directoryPath, "file.txt");

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(new[] { directoryPath });

        _directoryScannerMock
            .Setup(s => s.GetFiles(directoryPath, _options.SupportedExtensions))
            .Returns(new[] { filePath });

        _changeTrackerMock
            .Setup(t => t.ComputeFileHashAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("hash1");

        _documentIndexerMock
            .Setup(i => i.DocumentExistsAsync(filePath, expectedIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        _ = await _orchestrator.ProcessAllDirectoriesAsync(TestContext.Current.CancellationToken);

        // Assert
        _documentIndexerMock.Verify(
            i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void IndexingSummary_Add_MergesCountsCorrectly()
    {
        // Arrange
        IndexingOrchestrator.IndexingSummary summary1 = new()
        {
            TotalProcessed = 5,
            TotalAdded = 3,
            TotalUpdated = 2,
            TotalErrors = 0
        };

        IndexingOrchestrator.IndexingSummary summary2 = new()
        {
            TotalProcessed = 3,
            TotalAdded = 1,
            TotalUpdated = 1,
            TotalErrors = 1
        };

        // Act
        summary1.Add(summary2);

        // Assert
        summary1.TotalProcessed.ShouldBe(8);
        summary1.TotalAdded.ShouldBe(4);
        summary1.TotalUpdated.ShouldBe(3);
        summary1.TotalErrors.ShouldBe(1);
    }
}

