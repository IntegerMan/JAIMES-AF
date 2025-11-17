using MattEland.Jaimes.Indexer.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace MattEland.Jaimes.Tests.Services;

public class ChangeTrackerTests
{
    private readonly Mock<ILogger<ChangeTracker>> _loggerMock;
    private readonly ChangeTracker _tracker;

    public ChangeTrackerTests()
    {
        _loggerMock = new Mock<ILogger<ChangeTracker>>();
        _tracker = new ChangeTracker(_loggerMock.Object);
    }

    [Fact]
    public async Task ComputeFileHashAsync_WhenFileExists_ReturnsHash()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        string content = "Test content for hashing";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            string hash = await _tracker.ComputeFileHashAsync(tempFile, CancellationToken.None);

            // Assert
            hash.ShouldNotBeNull();
            hash.ShouldNotBeEmpty();
            hash.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("Content A")]
    [InlineData("Content B")]
    [InlineData("Different content entirely")]
    [InlineData("")]
    public async Task ComputeFileHashAsync_WhenContentChanges_ReturnsDifferentHash(string content)
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            string hash = await _tracker.ComputeFileHashAsync(tempFile, CancellationToken.None);

            // Assert
            hash.ShouldNotBeNull();
            hash.ShouldNotBeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeFileHashAsync_WhenSameContent_ReturnsSameHash()
    {
        // Arrange
        string content = "Identical content";
        string tempFile1 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        string tempFile2 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");

        await File.WriteAllTextAsync(tempFile1, content);
        await File.WriteAllTextAsync(tempFile2, content);

        try
        {
            // Act
            string hash1 = await _tracker.ComputeFileHashAsync(tempFile1, CancellationToken.None);
            string hash2 = await _tracker.ComputeFileHashAsync(tempFile2, CancellationToken.None);

            // Assert
            hash1.ShouldBe(hash2);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task ComputeFileHashAsync_WhenFileDoesNotExist_ThrowsException()
    {
        // Arrange
        string nonExistentFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(
            async () => await _tracker.ComputeFileHashAsync(nonExistentFile, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData("small.txt", "A")]
    [InlineData("medium.txt", "This is a medium-sized file with some content that should produce a hash.")]
    [InlineData("large.txt", "This is a larger file with repeated content. Content line. Content line. Content line. Content line. Content line. Content line. Content line. Content line. Content line. Content line.")]
    public async Task ComputeFileHashAsync_WithDifferentFileSizes_ReturnsValidHash(string fileName, string content)
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            string hash = await _tracker.ComputeFileHashAsync(tempFile, CancellationToken.None);

            // Assert
            hash.ShouldNotBeNull();
            hash.ShouldNotBeEmpty();
            // SHA256 base64 encoded should be 44 characters
            hash.Length.ShouldBe(44);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

