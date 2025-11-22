using System.Diagnostics;
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
    private readonly ActivitySource _activitySource;
    private readonly IndexingOrchestrator _orchestrator;

    public IndexingOrchestratorTests()
    {
        _loggerMock = new Mock<ILogger<IndexingOrchestrator>>();
        _directoryScannerMock = new Mock<IDirectoryScanner>();
        _documentIndexerMock = new Mock<IDocumentIndexer>();
        _activitySource = new ActivitySource("Jaimes.Indexer.Test");

        _options = new IndexerOptions
        {
            SourceDirectory = "C:\\Test\\DndDocuments",  // Include "dnd" to pass the filter
            VectorDbConnectionString = "Data Source=test.db",
            OllamaEndpoint = "http://localhost:11434",
            OllamaModel = "nomic-embed-text",
            SupportedExtensions = [".txt", ".md", ".pdf"]
        };

        _orchestrator = new IndexingOrchestrator(
            _loggerMock.Object,
            _directoryScannerMock.Object,
            _documentIndexerMock.Object,
            _options,
            _activitySource);
    }

    [Fact]
    public async Task ProcessAllDirectoriesAsync_WhenNoSubdirectories_ProcessesRootDirectoryOnly()
    {
        // Arrange
        string rootDir = _options.SourceDirectory;

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns([]);

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns([Path.Combine(rootDir, "file1.txt")]);

        string rootIndexName = GetIndexName(rootDir);
        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(Path.Combine(rootDir, "file1.txt"), rootIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("document-id-1");

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
            .Select(i => Path.Combine(rootDir, $"dndsubdir{i}"))  // Include "dnd" to pass the filter
            .ToList();

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns(subdirs);

        // Root directory has no files in this test
        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns([]);

        int subdirIndex = 0;
        foreach (string subdir in subdirs)
        {
            subdirIndex++;
            string indexName = GetIndexName(subdir);
            List<string> files = [];
            
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
                    .Setup(i => i.IndexDocumentAsync(file, indexName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync($"document-id-{file}");
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
            .Returns([]);

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns([filePath]);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, rootIndexName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Indexing failed"));

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
            .Returns([]);

        string rootIndexName = GetIndexName(rootDir);
        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns([filePath1, filePath2]);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath1, rootIndexName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("File access error"));

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath2, rootIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("document-id-2");

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
        string subdir = Path.Combine(rootDir, "dndsubdir1");  // Include "dnd" to pass the filter

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns([subdir]);

        _directoryScannerMock
            .Setup(s => s.GetFiles(subdir, _options.SupportedExtensions))
            .Returns([Path.Combine(subdir, "file1.txt")]);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        IndexingOrchestrator.IndexingSummary summary = await _orchestrator.ProcessAllDirectoriesAsync(cts.Token);

        // Assert
        summary.TotalProcessed.ShouldBe(0);
    }

    [Theory]
    [InlineData("C:\\Test\\DndRulesetA", "dndruleseta")]  // Include "dnd" to pass the filter
    [InlineData("C:\\Test\\DndRulesetB", "dndrulesetb")]  // Include "dnd" to pass the filter
    [InlineData("C:\\Test\\DndMy Rules", "dndmy rules")]  // Include "dnd" to pass the filter
    [InlineData("/home/test/dndrulesetA", "dndruleseta")]  // Include "dnd" to pass the filter
    public async Task GetIndexName_GeneratesCorrectIndexName(string directoryPath, string expectedIndexName)
    {
        // Arrange
        string rootDir = _options.SourceDirectory;
        string filePath = Path.Combine(directoryPath, "file.txt");

        _directoryScannerMock
            .Setup(s => s.GetSubdirectories(rootDir))
            .Returns([directoryPath]);

        _directoryScannerMock
            .Setup(s => s.GetFiles(directoryPath, _options.SupportedExtensions))
            .Returns([filePath]);

        _directoryScannerMock
            .Setup(s => s.GetFiles(rootDir, _options.SupportedExtensions))
            .Returns([]);

        _documentIndexerMock
            .Setup(i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("document-id");

        // Act
        _ = await _orchestrator.ProcessAllDirectoriesAsync(TestContext.Current.CancellationToken);

        // Assert
        _documentIndexerMock.Verify(
            i => i.IndexDocumentAsync(filePath, expectedIndexName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static string GetIndexName(string directoryPath)
    {
        // Replicate the logic from IndexingOrchestrator - uses directory name (lowercased) as ruleset tag
        DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
        return directoryInfo.Name.ToLowerInvariant();
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

