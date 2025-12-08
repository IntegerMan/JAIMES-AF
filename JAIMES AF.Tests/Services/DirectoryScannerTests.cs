using MattEland.Jaimes.DocumentProcessing.Services;

namespace MattEland.Jaimes.Tests.Services;

public class DirectoryScannerTests
{
    private readonly Mock<ILogger<DirectoryScanner>> _loggerMock;
    private readonly DirectoryScanner _scanner;

    public DirectoryScannerTests()
    {
        _loggerMock = new Mock<ILogger<DirectoryScanner>>();
        _scanner = new DirectoryScanner(_loggerMock.Object);
    }

    [Fact]
    public void GetSubdirectories_WhenDirectoryExists_ReturnsSubdirectories()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string subDir1 = Path.Combine(tempDir, "subdir1");
        string subDir2 = Path.Combine(tempDir, "subdir2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        try
        {
            // Act
            IEnumerable<string> result = _scanner.GetSubdirectories(tempDir);

            // Assert
            result.ShouldNotBeNull();
            List<string> subdirs = result.ToList();
            subdirs.Count.ShouldBe(2);
            subdirs.ShouldContain(subDir1);
            subdirs.ShouldContain(subDir2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetSubdirectories_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        Should.Throw<DirectoryNotFoundException>(() => _scanner.GetSubdirectories(nonExistentPath));
    }

    [Fact]
    public void GetSubdirectories_WhenDirectoryIsEmpty_ReturnsEmptyEnumerable()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            IEnumerable<string> result = _scanner.GetSubdirectories(tempDir);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".md", true)]
    [InlineData(".pdf", true)]
    [InlineData(".docx", true)]
    [InlineData(".TXT", true)] // Case insensitive
    [InlineData(".exe", false)]
    [InlineData(".dll", false)]
    public void GetFiles_WhenFileHasSupportedExtension_ReturnsFile(string extension, bool shouldBeIncluded)
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string testFile = Path.Combine(tempDir, $"test{extension}");
        File.WriteAllText(testFile, "test content");

        List<string> supportedExtensions = [".txt", ".md", ".pdf", ".docx"];

        try
        {
            // Act
            IEnumerable<string> result = _scanner.GetFiles(tempDir, supportedExtensions);

            // Assert
            result.ShouldNotBeNull();
            List<string> files = result.ToList();

            if (shouldBeIncluded)
                files.ShouldContain(testFile);
            else
                files.ShouldNotContain(testFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetFiles_WhenDirectoryDoesNotExist_ReturnsEmptyEnumerable()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        List<string> supportedExtensions = [".txt"];

        // Act
        IEnumerable<string> result = _scanner.GetFiles(nonExistentPath, supportedExtensions);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetFiles_WhenDirectoryHasMultipleFileTypes_ReturnsOnlySupportedExtensions()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        string txtFile = Path.Combine(tempDir, "test.txt");
        string mdFile = Path.Combine(tempDir, "test.md");
        string exeFile = Path.Combine(tempDir, "test.exe");
        string pdfFile = Path.Combine(tempDir, "test.pdf");

        File.WriteAllText(txtFile, "txt");
        File.WriteAllText(mdFile, "md");
        File.WriteAllText(exeFile, "exe");
        File.WriteAllText(pdfFile, "pdf");

        List<string> supportedExtensions = [".txt", ".md", ".pdf"];

        try
        {
            // Act
            IEnumerable<string> result = _scanner.GetFiles(tempDir, supportedExtensions);

            // Assert
            result.ShouldNotBeNull();
            List<string> files = result.ToList();
            files.Count.ShouldBe(3);
            files.ShouldContain(txtFile);
            files.ShouldContain(mdFile);
            files.ShouldContain(pdfFile);
            files.ShouldNotContain(exeFile);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}