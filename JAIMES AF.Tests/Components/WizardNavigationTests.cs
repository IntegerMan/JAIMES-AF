using Bunit;
using MattEland.Jaimes.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace MattEland.Jaimes.Tests.Components;

public class WizardNavigationTests : Bunit.TestContext
{
    public WizardNavigationTests()
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
    public void WizardNavigation_ShouldRenderCancelButton()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents"));

        // Assert
        cut.Markup.ShouldContain("Cancel");
        var cancelButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Href == "/agents");
        cancelButton.ShouldNotBeNull();
    }

    [Fact]
    public void WizardNavigation_ShouldShowBackButton_WhenShowBackIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowBack, true));

        // Assert
        cut.Markup.ShouldContain("Previous");
    }

    [Fact]
    public void WizardNavigation_ShouldHideBackButton_WhenShowBackIsFalse()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowBack, false));

        // Assert
        cut.Markup.ShouldNotContain("Previous");
    }

    [Fact]
    public void WizardNavigation_ShouldShowNextButton_WhenShowNextIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowNext, true));

        // Assert
        cut.Markup.ShouldContain("Next");
    }

    [Fact]
    public void WizardNavigation_ShouldHideNextButton_WhenShowNextIsFalse()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowNext, false));

        // Assert
        cut.Markup.ShouldNotContain("Next");
    }

    [Fact]
    public void WizardNavigation_ShouldUseCustomNextText()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowNext, true)
            .Add(p => p.NextText, "Generate"));

        // Assert
        cut.Markup.ShouldContain("Generate");
        cut.Markup.ShouldNotContain(">Next<");
    }

    [Fact]
    public async Task WizardNavigation_ShouldInvokeOnBack_WhenBackClicked()
    {
        // Arrange
        SetupMudProviders();
        var backClicked = false;

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowBack, true)
            .Add(p => p.OnBack, EventCallback.Factory.Create(this, () => backClicked = true)));

        var backButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Markup.Contains("Previous"));
        backButton.ShouldNotBeNull();
        await cut.InvokeAsync(() => backButton.Find("button").Click());

        // Assert
        backClicked.ShouldBeTrue();
    }

    [Fact]
    public async Task WizardNavigation_ShouldInvokeOnNext_WhenNextClicked()
    {
        // Arrange
        SetupMudProviders();
        var nextClicked = false;

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowNext, true)
            .Add(p => p.OnNext, EventCallback.Factory.Create(this, () => nextClicked = true)));

        var nextButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Markup.Contains("Next"));
        nextButton.ShouldNotBeNull();
        await cut.InvokeAsync(() => nextButton.Find("button").Click());

        // Assert
        nextClicked.ShouldBeTrue();
    }

    [Fact]
    public void WizardNavigation_ShouldDisableNext_WhenNextDisabledIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowNext, true)
            .Add(p => p.NextDisabled, true));

        // Assert
        var nextButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Markup.Contains("Next"));
        nextButton.ShouldNotBeNull();
        nextButton.Instance.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void WizardNavigation_ShouldUseCustomBackText()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.ShowBack, true)
            .Add(p => p.BackText, "Go Back"));

        // Assert
        cut.Markup.ShouldContain("Go Back");
        cut.Markup.ShouldNotContain(">Previous<");
    }

    [Fact]
    public void WizardNavigation_ShouldUseCustomCancelText()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<WizardNavigation>(parameters => parameters
            .Add(p => p.CancelHref, "/agents")
            .Add(p => p.CancelText, "Discard"));

        // Assert
        cut.Markup.ShouldContain("Discard");
        cut.Markup.ShouldNotContain(">Cancel<");
    }
}
