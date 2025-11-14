using MattEland.Jaimes.Web.Components.Helpers;
using Shouldly;

namespace MattEland.Jaimes.Tests.Helpers;

public class ChatHelpersTests
{
    #region GetAvatarInitials Tests

    [Theory]
    [InlineData(null, "?")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    public void GetAvatarInitials_ReturnsQuestionMark_ForInvalidInput(string? name, string expected)
    {
        // Act
        string result = ChatHelpers.GetAvatarInitials(name);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Game Master", "GM")]
    [InlineData("Player Character", "PC")]
    [InlineData("Emie von Laurentz", "EL")]
    [InlineData("Madonna", "MA")]
    [InlineData("John", "JO")]
    [InlineData("A", "A")]
    [InlineData("John    Doe", "JD")]
    [InlineData("  John Doe  ", "JD")]
    [InlineData("von der", "VD")]
    [InlineData("John-Doe123", "JO")]
    public void GetAvatarInitials_ReturnsExpectedInitials_ForValidNames(string name, string expected)
    {
        // Act
        string result = ChatHelpers.GetAvatarInitials(name);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("John van der Berg", "JB")]
    [InlineData("Maria de la Cruz", "MC")]
    [InlineData("Jean le Blanc", "JB")]
    public void GetAvatarInitials_IgnoresParticles(string name, string expected)
    {
        // Act
        string result = ChatHelpers.GetAvatarInitials(name);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region RenderMarkdown Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderMarkdown_ReturnsEmptyString_ForInvalidInput(string? markdown)
    {
        // Act
        string result = ChatHelpers.RenderMarkdown(markdown);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("**bold text**", "<strong>bold text</strong>")]
    [InlineData("*italic text*", "<em>italic text</em>")]
    [InlineData("# Header 1", "<h1>Header 1</h1>")]
    [InlineData("This is a paragraph.", "<p>This is a paragraph.</p>")]
    public void RenderMarkdown_ConvertsMarkdownToHtml(string markdown, string expectedHtml)
    {
        // Act
        string result = ChatHelpers.RenderMarkdown(markdown);

        // Assert
        result.ShouldContain(expectedHtml);
    }

    [Fact]
    public void RenderMarkdown_ConvertsCodeBlocks()
    {
        // Act
        string result = ChatHelpers.RenderMarkdown("```\ncode\n```");

        // Assert
        result.ShouldContain("<code>");
    }

    [Fact]
    public void RenderMarkdown_HandlesPlainText()
    {
        // Act
        string result = ChatHelpers.RenderMarkdown("Plain text without markdown");

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("Plain text without markdown");
    }

    #endregion

    #region SplitIntoParagraphs Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SplitIntoParagraphs_ReturnsEmpty_ForInvalidInput(string? text)
    {
        // Act
        IEnumerable<string> result = ChatHelpers.SplitIntoParagraphs(text);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SplitIntoParagraphs_ReturnsSingleParagraph_WhenNoNewlines()
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs("This is a single paragraph.").ToList();

        // Assert
        result.ShouldHaveSingleItem();
        result[0].ShouldBe("This is a single paragraph.");
    }

    [Theory]
    [InlineData("First paragraph.\n\nSecond paragraph.", 2, "First paragraph.", "Second paragraph.")]
    [InlineData("First paragraph.\r\n\r\nSecond paragraph.", 2, "First paragraph.", "Second paragraph.")]
    public void SplitIntoParagraphs_SplitsByDoubleNewlines(string text, int expectedCount, string firstParagraph, string secondParagraph)
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs(text).ToList();

        // Assert
        result.Count.ShouldBe(expectedCount);
        result[0].ShouldBe(firstParagraph);
        result[1].ShouldBe(secondParagraph);
    }

    [Theory]
    [InlineData("First line\nSecond line\nThird line", 3, "First line", "Second line", "Third line")]
    [InlineData("First line\r\nSecond line\r\nThird line", 3, "First line", "Second line", "Third line")]
    public void SplitIntoParagraphs_SplitsBySingleNewlines_WhenNoDoubleNewlines(string text, int expectedCount, string firstLine, string secondLine, string thirdLine)
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs(text).ToList();

        // Assert
        result.Count.ShouldBe(expectedCount);
        result[0].ShouldBe(firstLine);
        result[1].ShouldBe(secondLine);
        result[2].ShouldBe(thirdLine);
    }

    [Fact]
    public void SplitIntoParagraphs_TrimsWhitespace()
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs("  First paragraph  \n\n  Second paragraph  ").ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].ShouldBe("First paragraph");
        result[1].ShouldBe("Second paragraph");
    }

    [Fact]
    public void SplitIntoParagraphs_FiltersEmptyParagraphs()
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs("First paragraph\n\n\n\nSecond paragraph").ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].ShouldBe("First paragraph");
        result[1].ShouldBe("Second paragraph");
    }

    [Fact]
    public void SplitIntoParagraphs_HandlesMultipleParagraphs()
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs("Para 1\n\nPara 2\n\nPara 3\n\nPara 4").ToList();

        // Assert
        result.Count.ShouldBe(4);
        result[0].ShouldBe("Para 1");
        result[1].ShouldBe("Para 2");
        result[2].ShouldBe("Para 3");
        result[3].ShouldBe("Para 4");
    }

    [Fact]
    public void SplitIntoParagraphs_ReturnsOriginalText_WhenOnlyOneParagraphAfterSplitting()
    {
        // Act - Single newline should split, but if result is only one, return original trimmed
        List<string> result = ChatHelpers.SplitIntoParagraphs("Single paragraph with\nno real break").ToList();

        // Assert - Should split by single newline
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void SplitIntoParagraphs_HandlesMixedLineEndings()
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs("First\r\n\nSecond\n\r\nThird").ToList();

        // Assert
        result.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("First paragraph\n\nSecond paragraph\n\n", 2, "First paragraph", "Second paragraph")]
    [InlineData("\n\nFirst paragraph\n\nSecond paragraph", 2, "First paragraph", "Second paragraph")]
    public void SplitIntoParagraphs_HandlesLeadingAndTrailingNewlines(string text, int expectedCount, string firstParagraph, string secondParagraph)
    {
        // Act
        List<string> result = ChatHelpers.SplitIntoParagraphs(text).ToList();

        // Assert
        result.Count.ShouldBe(expectedCount);
        result[0].ShouldBe(firstParagraph);
        result[1].ShouldBe(secondParagraph);
    }

    #endregion
}
