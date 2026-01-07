using Bunit;
using MattEland.Jaimes.Web.Components.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace MattEland.Jaimes.Tests.Components;

public class InsightCardTests : Bunit.TestContext
{
    public InsightCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void SetupMudProviders()
    {
        RenderComponent<MudThemeProvider>();
        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
        RenderComponent<MudSnackbarProvider>();
    }

    [Fact]
    public void InsightCard_ShouldRenderTitle()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback"));

        // Assert
        cut.Markup.ShouldContain("Feedback");
    }

    [Fact]
    public void InsightCard_ShouldShowLoadingIndicator_WhenIsLoadingIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.IsLoading, true));

        // Assert
        cut.FindComponents<MudProgressCircular>().ShouldNotBeEmpty();
    }

    [Fact]
    public void InsightCard_ShouldNotShowLoadingIndicator_WhenIsLoadingIsFalse()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.IsLoading, false)
            .Add(p => p.Content, "Some content"));

        // Assert
        cut.FindComponents<MudProgressCircular>().ShouldBeEmpty();
    }

    [Fact]
    public void InsightCard_ShouldShowContent_WhenContentProvided()
    {
        // Arrange
        SetupMudProviders();
        const string content = "This is the insight content that should be displayed.";

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.Content, content));

        // Assert
        cut.Markup.ShouldContain(content);
    }

    [Fact]
    public void InsightCard_ShouldShowEmptyMessage_WhenNoContentAndNotLoading()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.IsLoading, false)
            .Add(p => p.Content, null)
            .Add(p => p.EmptyMessage, "No feedback data available."));

        // Assert
        cut.Markup.ShouldContain("No feedback data available.");
    }

    [Fact]
    public void InsightCard_ShouldShowCheckIcon_WhenContentProvided()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.Content, "Some content"));

        // Assert - Should have a success check icon
        var icons = cut.FindComponents<MudIcon>();
        icons.ShouldContain(i => i.Instance.Icon == Icons.Material.Filled.CheckCircle);
    }

    [Fact]
    public void InsightCard_ShouldNotShowCheckIcon_WhenNoContent()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.Content, null)
            .Add(p => p.IsLoading, false));

        // Assert - Should not have a success check icon
        var icons = cut.FindComponents<MudIcon>();
        icons.ShouldNotContain(i => i.Instance.Icon == Icons.Material.Filled.CheckCircle);
    }

    [Fact]
    public void InsightCard_ShouldUseCustomIcon()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Metrics")
            .Add(p => p.Icon, Icons.Material.Filled.PieChart));

        // Assert
        var icons = cut.FindComponents<MudIcon>();
        icons.ShouldContain(i => i.Instance.Icon == Icons.Material.Filled.PieChart);
    }

    [Fact]
    public void InsightCard_ShouldUseDefaultEmptyMessage()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.IsLoading, false)
            .Add(p => p.Content, null));

        // Assert
        cut.Markup.ShouldContain("No data available.");
    }

    [Fact]
    public void InsightCard_ShouldRenderAsMudPaper()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback"));

        // Assert
        cut.FindComponents<MudPaper>().ShouldNotBeEmpty();
    }

    [Fact]
    public void InsightCard_ShouldNotShowEmptyMessage_WhenLoading()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<InsightCard>(parameters => parameters
            .Add(p => p.Title, "Feedback")
            .Add(p => p.IsLoading, true)
            .Add(p => p.Content, null)
            .Add(p => p.EmptyMessage, "No data available."));

        // Assert - Empty message should not show while loading
        cut.Markup.ShouldNotContain("No data available.");
    }
}
