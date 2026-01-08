using MattEland.Jaimes.Web.Components.Helpers;
using Shouldly;

namespace MattEland.Jaimes.Tests.Helpers;

public class PromptHelpersTests
{
    [Fact]
    public void StripLeadingHeading_ShouldReturnEmpty_WhenInputIsEmpty()
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void StripLeadingHeading_ShouldReturnNull_WhenInputIsNull()
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void StripLeadingHeading_ShouldReturnWhitespace_WhenInputIsWhitespace()
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading("   ");

        // Assert
        result.ShouldBe("   ");
    }

    [Theory]
    [InlineData("## Improved Prompt\nActual content here", "Actual content here")]
    [InlineData("## Improved Prompt\r\nActual content here", "Actual content here")]
    [InlineData("# Improved Prompt\nActual content here", "Actual content here")]
    [InlineData("**Improved Prompt**\nActual content here", "Actual content here")]
    [InlineData("Improved Prompt:\nActual content here", "Actual content here")]
    public void StripLeadingHeading_ShouldRemoveHeading_WhenHeadingIsPresent(string input, string expected)
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("  ## Improved Prompt\nActual content here", "Actual content here")]
    [InlineData("\n## Improved Prompt\nActual content here", "Actual content here")]
    public void StripLeadingHeading_ShouldRemoveHeading_WhenLeadingWhitespacePresent(string input, string expected)
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("## improved prompt\nActual content here", "Actual content here")]
    [InlineData("## IMPROVED PROMPT\nActual content here", "Actual content here")]
    public void StripLeadingHeading_ShouldBeCaseInsensitive(string input, string expected)
    {
        // Arrange & Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void StripLeadingHeading_ShouldNotModify_WhenNoHeadingPresent()
    {
        // Arrange
        const string input = "You are a helpful AI assistant.";

        // Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe(input);
    }

    [Fact]
    public void StripLeadingHeading_ShouldNotRemove_HeadingInMiddleOfContent()
    {
        // Arrange
        const string input = "Some content\n## Improved Prompt\nMore content";

        // Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe(input);
    }

    [Fact]
    public void StripLeadingHeading_ShouldOnlyRemoveFirstHeading_WhenMultiplePresent()
    {
        // Arrange
        const string input = "## Improved Prompt\n# Improved Prompt\nActual content";

        // Act
        var result = PromptHelpers.StripLeadingHeading(input);

        // Assert
        result.ShouldBe("# Improved Prompt\nActual content");
    }
}
