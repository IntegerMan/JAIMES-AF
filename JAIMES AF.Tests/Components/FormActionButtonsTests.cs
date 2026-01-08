using Bunit;
using MattEland.Jaimes.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace MattEland.Jaimes.Tests.Components;

public class FormActionButtonsTests : Bunit.TestContext
{
    public FormActionButtonsTests()
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
    public void FormActionButtons_ShouldRenderPrimaryButton()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Create Game")
            .Add(p => p.CancelHref, "/games"));

        // Assert
        cut.Markup.ShouldContain("Create Game");
    }

    [Fact]
    public void FormActionButtons_ShouldRenderCancelButton()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games"));

        // Assert
        cut.Markup.ShouldContain("Cancel");
        var cancelButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Href == "/games");
        cancelButton.ShouldNotBeNull();
    }

    [Fact]
    public void FormActionButtons_ShouldShowSavingState_WhenIsSavingIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Create Game")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.IsSaving, true)
            .Add(p => p.SavingText, "Creating..."));

        // Assert
        cut.Markup.ShouldContain("Creating...");
        cut.Markup.ShouldNotContain(">Create Game<");
        // Should show progress indicator
        cut.FindComponents<MudProgressCircular>().ShouldNotBeEmpty();
    }

    [Fact]
    public void FormActionButtons_ShouldDisablePrimary_WhenIsSavingIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.IsSaving, true));

        // Assert
        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color != Color.Default);
        primaryButton.ShouldNotBeNull();
        primaryButton.Instance.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void FormActionButtons_ShouldDisableCancel_WhenIsSavingIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.IsSaving, true));

        // Assert
        var cancelButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color == Color.Default);
        cancelButton.ShouldNotBeNull();
        cancelButton.Instance.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void FormActionButtons_ShouldDisablePrimary_WhenPrimaryDisabledIsTrue()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.PrimaryDisabled, true));

        // Assert
        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color != Color.Default);
        primaryButton.ShouldNotBeNull();
        primaryButton.Instance.Disabled.ShouldBeTrue();
    }

    [Fact]
    public async Task FormActionButtons_ShouldInvokeOnPrimary_WhenPrimaryClicked()
    {
        // Arrange
        SetupMudProviders();
        var primaryClicked = false;

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.OnPrimary, EventCallback.Factory.Create(this, () => primaryClicked = true)));

        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color != Color.Default);
        primaryButton.ShouldNotBeNull();
        await cut.InvokeAsync(() => primaryButton.Find("button").Click());

        // Assert
        primaryClicked.ShouldBeTrue();
    }

    [Fact]
    public void FormActionButtons_ShouldUsePrimaryColor()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Create Agent")
            .Add(p => p.PrimaryColor, Color.Secondary)
            .Add(p => p.CancelHref, "/agents"));

        // Assert
        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color == Color.Secondary);
        primaryButton.ShouldNotBeNull();
    }

    [Fact]
    public void FormActionButtons_ShouldUseCustomCancelText()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Save")
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.CancelText, "Discard"));

        // Assert
        cut.Markup.ShouldContain("Discard");
        cut.Markup.ShouldNotContain(">Cancel<");
    }

    [Fact]
    public void FormActionButtons_ShouldShowPrimaryIcon_WhenNotSaving()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Create")
            .Add(p => p.PrimaryIcon, Icons.Material.Filled.Add)
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.IsSaving, false));

        // Assert - Icon should be present
        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Color != Color.Default);
        primaryButton.ShouldNotBeNull();
        primaryButton.Instance.StartIcon.ShouldBe(Icons.Material.Filled.Add);
    }

    [Fact]
    public void FormActionButtons_ShouldHidePrimaryIcon_WhenSaving()
    {
        // Arrange
        SetupMudProviders();

        // Act
        var cut = RenderComponent<FormActionButtons>(parameters => parameters
            .Add(p => p.PrimaryText, "Create")
            .Add(p => p.PrimaryIcon, Icons.Material.Filled.Add)
            .Add(p => p.CancelHref, "/games")
            .Add(p => p.IsSaving, true));

        // Assert - Icon should be null when saving (replaced by progress circular)
        var primaryButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(b => b.Instance.Variant == Variant.Filled);
        primaryButton.ShouldNotBeNull();
        primaryButton.Instance.StartIcon.ShouldBeNull();
    }
}
