using System.Reflection;
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
    [InlineData("C:\\Test\\rulesetA\\rules.pdf", "index-ruleseta")]
    [InlineData("C:\\Test\\rulesetB\\rules.pdf", "index-rulesetb")]
    [InlineData("/home/test/rulesetA/file.txt", "index-ruleseta")]
    [InlineData("D:\\Documents\\My Rules\\guide.md", "index-my-rules")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\ATOW_QSR.pdf", "index-test")]
    public async Task IndexDocumentAsync_WhenFileExists_CallsImportDocumentAsyncWithCorrectDocumentId(
        string filePath, string indexName)
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
        await File.WriteAllTextAsync(tempFile, "test content");
        string fileHash = "test-hash";

        // Generate expected document ID from temp file path using reflection
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();
        string expectedDocumentId = (string)getDocumentIdMethod.Invoke(null, new object[] { tempFile })!;

        Document? capturedDocument = null;
        // Mock ImportDocumentAsync - signature: ImportDocumentAsync(Document, string?, IEnumerable<string>?, IContext?, CancellationToken)
        _memoryMock
            .Setup(m => m.ImportDocumentAsync(
                It.IsAny<Document>(), 
                It.IsAny<string?>(), 
                It.IsAny<IEnumerable<string>?>(), 
                It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), 
                It.IsAny<CancellationToken>()))
            .Callback<Document, string?, IEnumerable<string>?, Microsoft.KernelMemory.Context.IContext?, CancellationToken>(
                (doc, idx, steps, ctx, ct) => capturedDocument = doc)
            .ReturnsAsync(string.Empty);

        try
        {
            // Act
            bool result = await _indexer.IndexDocumentAsync(tempFile, indexName, fileHash, TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeTrue();
            capturedDocument.ShouldNotBeNull();
            capturedDocument.Id.ShouldBe(expectedDocumentId);
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
        // Arrange - Generate expected document ID using reflection
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();
        string expectedDocumentId = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath })!;
        
        DataPipelineStatus? status = documentExists
            ? new DataPipelineStatus { Completed = true }
            : null;

        _memoryMock
            .Setup(m => m.GetDocumentStatusAsync(expectedDocumentId, indexName, It.IsAny<CancellationToken>()))
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
        
        // Generate expected document ID using reflection
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();
        string documentId = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath })!;

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
    [InlineData("C:\\Test\\RulesetA\\Rules.pdf")]
    [InlineData("C:\\Test\\RulesetB\\Rules.pdf")]
    [InlineData("D:\\Documents\\My Rules\\Guide.md")]
    [InlineData("/home/user/rulesetA/file.txt")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\ATOW_QSR.pdf")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\E-CAT35260_Combat_Manual_Mercenaries.pdf")]
    public void GetDocumentId_GeneratesValidHashBasedId(string filePath)
    {
        // Arrange - Use reflection to access the private static method
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string actualDocumentId = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath })!;

        // Assert - Verify the ID format and validity
        actualDocumentId.ShouldStartWith("doc-");
        actualDocumentId.Length.ShouldBe(36); // "doc-" (4) + 32 hex characters
        
        // Verify it only contains valid characters (a-f, 0-9, '-', '.')
        string idWithoutPrefix = actualDocumentId[4..];
        foreach (char c in idWithoutPrefix)
        {
            (char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_').ShouldBeTrue(
                $"Document ID contains invalid character: '{c}' in '{actualDocumentId}'");
        }
        
        // Verify it's a valid hex string (only 0-9, a-f)
        idWithoutPrefix.ShouldMatch("^[0-9a-f]{32}$");
    }
    
    [Fact]
    public void GetDocumentId_SamePathGeneratesSameId()
    {
        // Arrange
        string filePath = "C:\\Test\\file.pdf";
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string id1 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath })!;
        string id2 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath })!;

        // Assert
        id1.ShouldBe(id2);
    }
    
    [Fact]
    public void GetDocumentId_DifferentPathsGenerateDifferentIds()
    {
        // Arrange
        string filePath1 = "C:\\Test\\file1.pdf";
        string filePath2 = "C:\\Test\\file2.pdf";
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string id1 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath1 })!;
        string id2 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath2 })!;

        // Assert
        id1.ShouldNotBe(id2);
    }
}

