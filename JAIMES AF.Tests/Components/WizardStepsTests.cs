using Bunit;
using MattEland.Jaimes.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace MattEland.Jaimes.Tests.Components;

public class WizardStepsTests : Bunit.TestContext
{
    public WizardStepsTests()
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
    public void WizardSteps_ShouldRenderAllStepTitles()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Insights", "Feedback", "Generate", "Apply" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 0)
            .Add(p => p.MaxReachedStep, 0));

        // Assert
        foreach (var title in stepTitles)
        {
            cut.Markup.ShouldContain(title);
        }
    }

    [Fact]
    public void WizardSteps_ShouldShowActiveStepWithPrimaryColor()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 1)
            .Add(p => p.MaxReachedStep, 1));

        // Assert - Active step should have Primary color avatar
        var avatars = cut.FindComponents<MudAvatar>();
        avatars.Count.ShouldBe(3);
        avatars[1].Instance.Color.ShouldBe(Color.Primary);
    }

    [Fact]
    public void WizardSteps_ShouldShowCompletedStepsWithCheckIcon()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 2)
            .Add(p => p.MaxReachedStep, 2));

        // Assert - Completed steps (0 and 1) should have Success color
        var avatars = cut.FindComponents<MudAvatar>();
        avatars[0].Instance.Color.ShouldBe(Color.Success);
        avatars[1].Instance.Color.ShouldBe(Color.Success);
        // Active step should be Primary
        avatars[2].Instance.Color.ShouldBe(Color.Primary);
    }

    [Fact]
    public void WizardSteps_ShouldShowFutureStepsWithDefaultColor()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 0)
            .Add(p => p.MaxReachedStep, 0));

        // Assert - Future steps should have Default color
        var avatars = cut.FindComponents<MudAvatar>();
        avatars[1].Instance.Color.ShouldBe(Color.Default);
        avatars[2].Instance.Color.ShouldBe(Color.Default);
    }

    [Fact]
    public void WizardSteps_ShouldRenderDividersBetweenSteps()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 0)
            .Add(p => p.MaxReachedStep, 0));

        // Assert - Should have n-1 dividers for n steps
        var dividers = cut.FindComponents<MudDivider>();
        dividers.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WizardSteps_ShouldInvokeOnStepClick_WhenClickingReachableStep()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };
        int? clickedStep = null;

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 2)
            .Add(p => p.MaxReachedStep, 2)
            .Add(p => p.OnStepClick, EventCallback.Factory.Create<int>(this, step => clickedStep = step)));

        // Click on step 0 (which is reachable and not active) - find by cursor-pointer class
        var clickableElements = cut.FindAll(".cursor-pointer");
        clickableElements.Count.ShouldBeGreaterThan(0, "Should have at least one clickable step");
        
        await cut.InvokeAsync(() => clickableElements[0].Click());

        // Assert
        clickedStep.ShouldBe(0);
    }

    [Fact]
    public void WizardSteps_ShouldShowStepNumbers_ForNonCompletedSteps()
    {
        // Arrange
        SetupMudProviders();
        var stepTitles = new[] { "Step 1", "Step 2", "Step 3" };

        // Act
        var cut = RenderComponent<WizardSteps>(parameters => parameters
            .Add(p => p.StepTitles, stepTitles)
            .Add(p => p.ActiveStep, 0)
            .Add(p => p.MaxReachedStep, 0));

        // Assert - Should show step numbers 1, 2, 3
        cut.Markup.ShouldContain(">1<");
        cut.Markup.ShouldContain(">2<");
        cut.Markup.ShouldContain(">3<");
    }
}
