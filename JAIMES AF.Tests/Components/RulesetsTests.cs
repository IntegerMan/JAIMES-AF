using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Moq.Protected;
using System.Net.Http.Json;

namespace MattEland.Jaimes.Tests.Components;

public class RulesetsTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public RulesetsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton(_loggerFactoryMock.Object);
        Services.AddSingleton(_dialogServiceMock.Object);

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
    }

    private void SetupMudProviders()
    {
        RenderComponent<MudThemeProvider>();
        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
        RenderComponent<MudSnackbarProvider>();
    }

    private static RulesetInfoResponse CreateTestRuleset(
        string id = "dnd5e",
        string name = "Dungeons and Dragons 5th Edition",
        string? description = null,
        int sourcebookCount = 0)
    {
        return new RulesetInfoResponse
        {
            Id = id,
            Name = name,
            Description = description,
            SourcebookCount = sourcebookCount
        };
    }

    private HttpClient CreateMockHttpClient(RulesetInfoResponse[] rulesets)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = new RulesetListResponse { Rulesets = rulesets };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/rulesets")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(response),
            });

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public void RulesetsPage_ShouldShowRulesetCount_WhenRulesetsExist()
    {
        // Arrange
        var rulesets = new[]
        {
            CreateTestRuleset("dnd5e", "Dungeons and Dragons 5th Edition"),
            CreateTestRuleset("pathfinder2e", "Pathfinder 2nd Edition"),
            CreateTestRuleset("coc7e", "Call of Cthulhu 7th Edition")
        };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("3 rulesets available");
    }

    [Fact]
    public void RulesetsPage_ShouldShowSingularCount_WhenOneRulesetExists()
    {
        // Arrange
        var rulesets = new[] { CreateTestRuleset() };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("1 ruleset available");
        cut.Markup.ShouldNotContain("1 rulesets available");
    }

    [Fact]
    public void RulesetsPage_ShouldShowEmptyState_WhenNoRulesets()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No rulesets yet");
        cut.Markup.ShouldContain("Create Your First Ruleset");
        cut.Markup.ShouldContain("game mechanics");
    }

    [Fact]
    public void RulesetsPage_ActionButtons_ShouldHaveTooltipsWithPlacementTop()
    {
        // Arrange
        var rulesets = new[] { CreateTestRuleset() };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find all MudTooltip components
        var tooltips = cut.FindComponents<MudTooltip>();
        tooltips.ShouldNotBeEmpty("Action buttons should be wrapped in tooltips");

        // All tooltips should have Placement.Top
        foreach (var tooltip in tooltips)
        {
            tooltip.Instance.Placement.ShouldBe(Placement.Top, "All tooltips should have Placement.Top");
        }
    }

    [Fact]
    public void RulesetsPage_ShouldRenderNewRulesetButton()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("New Ruleset");
    }

    [Fact]
    public void RulesetsPage_ShouldShowHeroSection_WithCorrectTitle()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("Your Rulesets");
    }

    [Fact]
    public void RulesetsPage_ShouldHaveEditButton_WithTooltip()
    {
        // Arrange
        var rulesets = new[] { CreateTestRuleset() };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find edit button
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit);
        editButtons.ShouldNotBeEmpty("Edit button should exist");
    }

    [Fact]
    public void RulesetsPage_RulesetNameLink_ShouldNavigateToEditPage()
    {
        // Arrange
        var ruleset = CreateTestRuleset("dnd5e", "Dungeons and Dragons 5th Edition");
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify ruleset name is a link to edit page
        var links = cut.FindComponents<MudLink>();
        var rulesetLink = links.FirstOrDefault(l => l.Markup.Contains("Dungeons and Dragons 5th Edition"));
        rulesetLink.ShouldNotBeNull();
        (rulesetLink.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void RulesetsPage_EditButton_ShouldLinkToEditPage()
    {
        // Arrange
        var ruleset = CreateTestRuleset("dnd5e", "Test Ruleset");
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify edit button links to edit page
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit)
            .ToList();
        editButtons.ShouldNotBeEmpty();

        var editButton = editButtons.First();
        (editButton.Instance.Href ?? "").ShouldContain(ruleset.Id);
        (editButton.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void RulesetsPage_ShouldShowShortNameColumn_WithRulesetId()
    {
        // Arrange
        var ruleset = CreateTestRuleset("dnd5e", "Test Ruleset");
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify Short Name column header and RulesetLink component
        cut.Markup.ShouldContain("Short Name");
        cut.Markup.ShouldContain("dnd5e"); // The ID should be displayed via RulesetLink
    }

    [Fact]
    public void RulesetsPage_ShouldShowCreateMessage_WhenNoRulesets()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Hero section should show encouraging message
        cut.Markup.ShouldContain("Create your first ruleset");
    }

    [Fact]
    public void RulesetsPage_ShouldDisplayDescriptionColumn_WhenDescriptionExists()
    {
        // Arrange
        var ruleset = CreateTestRuleset(description: "The classic tabletop RPG system");
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("Description"); // Column header
        cut.Markup.ShouldContain("The classic tabletop RPG system");
    }

    [Fact]
    public void RulesetsPage_ShouldShowDash_WhenDescriptionIsNull()
    {
        // Arrange
        var ruleset = CreateTestRuleset(description: null);
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Description column should show "-" for null
        cut.Markup.ShouldContain(">-<"); // The "-" placeholder
    }

    [Fact]
    public void RulesetsPage_ShouldDisplaySourcebooksColumn()
    {
        // Arrange
        var ruleset = CreateTestRuleset(sourcebookCount: 5);
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("Sourcebooks"); // Column header
        cut.Markup.ShouldContain(">5<"); // The count value
    }

    [Fact]
    public void RulesetsPage_SourcebooksChip_ShouldHaveViewSourcebooksTooltip()
    {
        // Arrange
        var ruleset = CreateTestRuleset(sourcebookCount: 3);
        var rulesets = new[] { ruleset };
        Services.AddSingleton(CreateMockHttpClient(rulesets));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Rulesets>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find tooltip for sourcebooks
        var tooltips = cut.FindComponents<MudTooltip>()
            .Where(t => t.Instance.Text == "View Sourcebooks");
        tooltips.ShouldNotBeEmpty("Sourcebooks chip should have a tooltip");
    }
}
