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
    private readonly Mock<IDocumentIndexer> _documentIndexerMock;
    private readonly IndexerOptions _options;
    private readonly IndexingOrchestrator _orchestrator;

    public IndexingOrchestratorTests()
    {
        _loggerMock = new Mock<ILogger<IndexingOrchestrator>>();
        _directoryScannerMock = new Mock<IDirectoryScanner>();
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
            _documentIndexerMock.Object,
            _options);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenNoSubdirectories_ProcessesRootDirectoryOnly()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { Path.Combine(rootDir, "file1.txt") });

        string rootIndexName = GetIndexName(rootDir);
        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(Path.Combine(rootDir, "file1.txt"), rootIndexName, It.IsAny<CancellationToken>()));

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        summary.TotalProcessed.ShouldBe(1);
        summary.TotalSucceeded.ShouldBe(1);
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

        // Root directory has no files in this test
        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(Enumerable.Empty<string>());

        int subdirIndex = 0;
        foreach (string subdir in subdirs)
        {
            subdirIndex++;
            string indexName = GetIndexName(subdir);
            List<string> files = new();
            
            // Add new files
            for (int i = 0; i < newFiles; i++)
            {
                files.Add(Path.Combine(subdir, $"file{i}.txt"));
            }
            
            // Add existing files
            for (int i = 0; i < existingFiles; i++)
            {
                files.Add(Path.Combine(subdir, $"existing{i}.txt"));
            }

            _directoryScannerMock
                .Setup(s => s.GetFiles(subdir, _options.SupportedExtensions))
                .Returns(files);

            foreach (string file in files)
            {
                _documentIndexerMock
                    .Setup(i => i.IndexDocumentAsync(file, indexName, It.IsAny<CancellationToken>()));
            }
        }

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        // Note: The orchestrator processes subdirectories AND root directory
        // Since root directory has no files in this test, only subdirectory files are processed
        int totalFiles = subdirectoryCount * (newFiles + existingFiles);
        summary.TotalProcessed.ShouldBe(totalFiles);
        summary.TotalSucceeded.ShouldBe(totalFiles);
        summary.TotalErrors.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenFileIndexingFails_IncrementsErrorCount()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath = Path.Combine(rootDir, "file.txt");
        string rootIndexName = GetIndexName(rootDir);

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { filePath });

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, rootIndexName, It.IsAny<CancellationToken>())); // Indexing fails

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        summary.TotalProcessed.ShouldBe(1);
        summary.TotalErrors.ShouldBe(1);
        summary.TotalSucceeded.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenExceptionThrown_LogsErrorAndContinues()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath1 = Path.Combine(rootDir, "file1.txt");
        string filePath2 = Path.Combine(rootDir, "file2.txt");

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(Enumerable.Empty<string>());

        string rootIndexName = GetIndexName(rootDir);
        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(new[] { filePath1, filePath2 });

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath1, rootIndexName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("File access error"));

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath2, rootIndexName, It.IsAny<CancellationToken>()));

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(CancellationToken.None);

        // Assert
        // Both files are processed: file1 throws exception (error), file2 succeeds
        summary.TotalProcessed.ShouldBe(2);
        summary.TotalErrors.ShouldBe(1); // file1 exception
        summary.TotalSucceeded.ShouldBe(1); // file2 succeeds
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

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns(Enumerable.Empty<string>());

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<CancellationToken>()));

        // Act
        _ = await _orchestrator.ProcessAllDirectoriesAsync(TestContext.Current.CancellationToken);

        // Assert
        _documentIndexerMock.Verify(
            i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string GetIndexName(string directoryPath)
    {
        // Replicate the logic from IndexingOrchestrator.GetIndexName
        string directoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(directoryName))
        {
            directoryName = "root";
        }
        return $"index-{directoryName.ToLowerInvariant().Replace(" ", "-")}";
    }

    [Fact]
    public void IndexingSummary_Add_MergesCountsCorrectly()
    {
        // Arrange
        IndexingOrchestrator.IndexingSummary summary1 = new()
        {
            TotalProcessed = 5,
            TotalSucceeded = 3,
            TotalErrors = 0
        };

        IndexingOrchestrator.IndexingSummary summary2 = new()
        {
            TotalProcessed = 3,
            TotalSucceeded = 1,
            TotalErrors = 1
        };

        // Act
        summary1.Add(summary2);

        // Assert
        summary1.TotalProcessed.ShouldBe(8);
        summary1.TotalSucceeded.ShouldBe(4);
        summary1.TotalErrors.ShouldBe(1);
    }
}

