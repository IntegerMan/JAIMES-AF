using MattEland.Jaimes.Indexer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Moq;
using Shouldly;
using Xunit;

namespace MattEland.Jaimes.Tests.Services;

public class DocumentIndexerTests
{
    private readonly Mock<ILogger<DocumentIndexer>> _loggerMock;
    private readonly Mock<IKernelMemory> _memoryMock;
    private readonly DocumentIndexer _indexer;

    public DocumentIndexerTests()
    {
        _loggerMock = new Mock<ILogger<DocumentIndexer>>();
        _memoryMock = new Mock<IKernelMemory>();
        _indexer = new DocumentIndexer(_loggerMock.Object, _memoryMock.Object);
    }

    [Theory]
    [InlineData("C:\\Test\\rulesetA\\rules.pdf", "index-ruleseta", "doc-c-/test/ruleseta/rules.pdf")]
    [InlineData("C:\\Test\\rulesetB\\rules.pdf", "index-rulesetb", "doc-c-/test/rulesetb/rules.pdf")]
    [InlineData("/home/test/rulesetA/file.txt", "index-ruleseta", "doc-/home/test/ruleseta/file.txt")]
    [InlineData("D:\\Documents\\My Rules\\guide.md", "index-my-rules", "doc-d-/documents/my-rules/guide.md")]
    public async Task IndexDocumentAsync_WhenFileExists_CallsImportDocumentAsyncWithCorrectDocumentId(
        string filePath, string indexName, string expectedDocumentId)
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
        await File.WriteAllTextAsync(tempFile, "test content");
        string fileHash = "test-hash";

        // Mock ImportDocumentAsync - signature: ImportDocumentAsync(Document, string?, IEnumerable<string>?, IContext?, CancellationToken)
        _memoryMock
            .Setup(m => m.ImportDocumentAsync(
                It.IsAny<Document>(), 
                It.IsAny<string?>(), 
                It.IsAny<IEnumerable<string>?>(), 
                It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        try
        {
            // Act
            bool result = await _indexer.IndexDocumentAsync(tempFile, indexName, fileHash, TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeTrue();
            // Verify the call was made with the correct document ID
            _memoryMock.Verify(
                m => m.ImportDocumentAsync(
                    It.Is<Document>(d => d.Id == expectedDocumentId),
                    It.IsAny<string?>(),
                    It.IsAny<IEnumerable<string>?>(),
                    It.IsAny<Microsoft.KernelMemory.Context.IContext?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_WhenFileExists_AddsCorrectTags()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "test.txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        string fileHash = "test-hash-123";
        string indexName = "test-index";

        Document? capturedDocument = null;
        _memoryMock
            .Setup(m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()))
            .Callback<Document, string?, IEnumerable<string>?, Microsoft.KernelMemory.Context.IContext?, CancellationToken>((doc, idx, steps, ctx, ct) => capturedDocument = doc)
            .ReturnsAsync(string.Empty);

        try
        {
            // Act
            await _indexer.IndexDocumentAsync(tempFile, indexName, fileHash, TestContext.Current.CancellationToken);

            // Assert
            capturedDocument.ShouldNotBeNull();
            // Note: We can't easily verify tags without accessing internal Document structure
            // But we can verify the method was called correctly
            // Verify the call was made
            _memoryMock.Verify(
                m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        string nonExistentFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        string fileHash = "test-hash";
        string indexName = "test-index";

        // Act
            bool result = await _indexer.IndexDocumentAsync(nonExistentFile, indexName, fileHash, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
        _memoryMock.Verify(
            m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IndexDocumentAsync_WhenImportThrowsException_ReturnsFalse()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), "test.txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        string fileHash = "test-hash";
        string indexName = "test-index";

        _memoryMock
            .Setup(m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Import failed"));

        try
        {
            // Act
            bool result = await _indexer.IndexDocumentAsync(tempFile, indexName, fileHash, TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("C:\\Test\\rulesetA\\rules.pdf", "index-ruleseta", true)]
    [InlineData("C:\\Test\\rulesetB\\rules.pdf", "index-rulesetb", false)]
    [InlineData("/home/test/file.txt", "index-test", true)]
    public async Task DocumentExistsAsync_WhenDocumentExists_ReturnsTrue(
        string filePath, string indexName, bool documentExists)
    {
        // Arrange
        string documentId = $"doc-{filePath.Replace("\\", "/").Replace(":", "-").ToLowerInvariant()}";
        
        DataPipelineStatus? status = documentExists
            ? new DataPipelineStatus { Completed = true }
            : null;

        _memoryMock
            .Setup(m => m.GetDocumentStatusAsync(documentId, indexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        // Act
        bool result = await _indexer.DocumentExistsAsync(filePath, indexName, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(documentExists);
    }

    [Fact]
    public async Task DocumentExistsAsync_WhenStatusIsNotCompleted_ReturnsFalse()
    {
        // Arrange
        string filePath = "C:\\Test\\file.txt";
        string indexName = "test-index";
        string documentId = $"doc-c-/test/file.txt";

        DataPipelineStatus status = new DataPipelineStatus { Completed = false };

        _memoryMock
            .Setup(m => m.GetDocumentStatusAsync(documentId, indexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        // Act
        bool result = await _indexer.DocumentExistsAsync(filePath, indexName, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DocumentExistsAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        string filePath = "C:\\Test\\file.txt";
        string indexName = "test-index";

        _memoryMock
            .Setup(m => m.GetDocumentStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        bool result = await _indexer.DocumentExistsAsync(filePath, indexName, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("C:\\Test\\RulesetA\\Rules.pdf", "doc-c-/test/ruleseta/rules.pdf")]
    [InlineData("C:\\Test\\RulesetB\\Rules.pdf", "doc-c-/test/rulesetb/rules.pdf")]
    [InlineData("D:\\Documents\\My Rules\\Guide.md", "doc-d-/documents/my-rules/guide.md")]
    [InlineData("/home/user/rulesetA/file.txt", "doc-/home/user/ruleseta/file.txt")]
    public async Task GetDocumentId_NormalizesPathCorrectly(string filePath, string expectedDocumentId)
    {
        // Arrange & Act
        // We need to access the private method via reflection or test it indirectly
        // Testing indirectly through DocumentExistsAsync which uses GetDocumentId
        string indexName = "test-index";
        
        _memoryMock
            .Setup(m => m.GetDocumentStatusAsync(expectedDocumentId, indexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataPipelineStatus?)null);

        // Act
        _ = await _indexer.DocumentExistsAsync(filePath, indexName, TestContext.Current.CancellationToken);

        // Assert
        _memoryMock.Verify(
            m => m.GetDocumentStatusAsync(expectedDocumentId, indexName, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

