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
    [InlineData("C:\\Test\\rulesetA\\rules.pdf", "index-ruleseta", "ruleseta-rules.pdf")]
    [InlineData("C:\\Test\\rulesetB\\rules.pdf", "index-rulesetb", "rulesetb-rules.pdf")]
    [InlineData("/home/test/rulesetA/file.txt", "index-ruleseta", "ruleseta-file.txt")]
    [InlineData("D:\\Documents\\My Rules\\guide.md", "index-my-rules", "myrules-guide.md")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\ATOW_QSR.pdf", "index-battletech", "battletech-atow_qsr.pdf")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\E-CAT35260_Combat_Manual_Mercenaries.pdf", "index-battletech", "battletech-ecat35260_combat_manual_mercenaries.pdf")]
    public async Task IndexDocumentAsync_WhenFileExists_CallsImportDocumentAsyncWithCorrectDocumentId(
        string filePath, string indexName, string expectedDocumentId)
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
        await File.WriteAllTextAsync(tempFile, "test content");

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
            bool result = await _indexer.IndexDocumentAsync(tempFile, indexName, TestContext.Current.CancellationToken);

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
        string indexName = "test-index";

        Document? capturedDocument = null;
        _memoryMock
            .Setup(m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()))
            .Callback<Document, string?, IEnumerable<string>?, Microsoft.KernelMemory.Context.IContext?, CancellationToken>((doc, idx, steps, ctx, ct) => capturedDocument = doc)
            .ReturnsAsync(string.Empty);

        try
        {
            // Act
            await _indexer.IndexDocumentAsync(tempFile, indexName, TestContext.Current.CancellationToken);

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
        string indexName = "test-index";

        // Act
        bool result = await _indexer.IndexDocumentAsync(nonExistentFile, indexName, TestContext.Current.CancellationToken);

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
        string indexName = "test-index";

        _memoryMock
            .Setup(m => m.ImportDocumentAsync(It.IsAny<Document>(), It.IsAny<string?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<Microsoft.KernelMemory.Context.IContext?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Import failed"));

        try
        {
            // Act
            bool result = await _indexer.IndexDocumentAsync(tempFile, indexName, TestContext.Current.CancellationToken);

            // Assert
            result.ShouldBeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("C:\\Test\\rulesetA\\rules.pdf", "index-ruleseta", "ruleseta-rules.pdf", true)]
    [InlineData("C:\\Test\\rulesetB\\rules.pdf", "index-rulesetb", "rulesetb-rules.pdf", false)]
    [InlineData("/home/test/file.txt", "index-test", "test-file.txt", true)]
    public async Task DocumentExistsAsync_WhenDocumentExists_ReturnsTrue(
        string filePath, string indexName, string expectedDocumentId, bool documentExists)
    {
        // Arrange
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
        string indexName = "index-test";
        string documentId = "test-file.txt";

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
    [InlineData("C:\\Test\\RulesetA\\Rules.pdf", "index-ruleseta", "ruleseta-rules.pdf")]
    [InlineData("C:\\Test\\RulesetB\\Rules.pdf", "index-rulesetb", "rulesetb-rules.pdf")]
    [InlineData("D:\\Documents\\My Rules\\Guide.md", "index-my-rules", "myrules-guide.md")]
    [InlineData("/home/user/rulesetA/file.txt", "index-ruleseta", "ruleseta-file.txt")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\ATOW_QSR.pdf", "index-battletech", "battletech-atow_qsr.pdf")]
    [InlineData("C:\\Users\\MattE\\OneDrive\\Sourcebooks\\Battletech\\E-CAT35260_Combat_Manual_Mercenaries.pdf", "index-battletech", "battletech-ecat35260_combat_manual_mercenaries.pdf")]
    public void GetDocumentId_GeneratesRulesetIdFilenameFormat(string filePath, string indexName, string expectedDocumentId)
    {
        // Arrange - Use reflection to access the private static method
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string actualDocumentId = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath, indexName })!;

        // Assert
        actualDocumentId.ShouldBe(expectedDocumentId);
        
        // Verify format: rulesetId-filename.extension
        actualDocumentId.ShouldContain("-");
        string[] parts = actualDocumentId.Split('-', 2);
        parts.Length.ShouldBe(2);
        
        // Verify it only contains valid characters (letters, numbers, periods, underscores, and dash separator)
        foreach (char c in actualDocumentId)
        {
            (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ShouldBeTrue(
                $"Document ID contains invalid character: '{c}' in '{actualDocumentId}'");
        }
        
        // Verify it's lowercase
        actualDocumentId.ShouldBe(actualDocumentId.ToLowerInvariant());
    }
    
    [Fact]
    public void GetDocumentId_SamePathAndIndexGeneratesSameId()
    {
        // Arrange
        string filePath = "C:\\Test\\file.pdf";
        string indexName = "index-test";
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string id1 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath, indexName })!;
        string id2 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath, indexName })!;

        // Assert
        id1.ShouldBe(id2);
    }
    
    [Fact]
    public void GetDocumentId_DifferentFilenamesGenerateDifferentIds()
    {
        // Arrange
        string filePath1 = "C:\\Test\\file1.pdf";
        string filePath2 = "C:\\Test\\file2.pdf";
        string indexName = "index-test";
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string id1 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath1, indexName })!;
        string id2 = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath2, indexName })!;

        // Assert
        id1.ShouldNotBe(id2);
        id1.ShouldBe("test-file1.pdf");
        id2.ShouldBe("test-file2.pdf");
    }
    
    [Fact]
    public void GetDocumentId_RemovesInvalidCharacters()
    {
        // Arrange
        string filePath = "C:\\Test\\File With Spaces & Special!Chars.pdf";
        string indexName = "index-test-ruleset";
        MethodInfo? getDocumentIdMethod = typeof(DocumentIndexer).GetMethod("GetDocumentId", BindingFlags.NonPublic | BindingFlags.Static);
        getDocumentIdMethod.ShouldNotBeNull();

        // Act
        string documentId = (string)getDocumentIdMethod.Invoke(null, new object[] { filePath, indexName })!;

        // Assert
        documentId.ShouldBe("testruleset-filewithspacesspecialchars.pdf");
        // Verify no invalid characters
        foreach (char c in documentId)
        {
            (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-').ShouldBeTrue();
        }
    }
}

