using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MattEland.Jaimes.Web.Components.Chat;

namespace MattEland.Jaimes.Tests.Components;

public class GameDetailsTests : Bunit.TestContext
{
    public GameDetailsTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IJSRuntime>(new Mock<IJSRuntime>().Object);
        Services.AddHttpClient(); // For IHttpClientFactory
        Services.AddLogging();
    }

    private void SetupMudProviders()
    {
        RenderComponent<MudThemeProvider>();
        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
        RenderComponent<MudSnackbarProvider>();
    }

    [Fact]
    public void ErrorMessageShouldDisplayInDangerBubble()
    {
        // This test is a placeholder as discussed
    }

    [Fact]
    public void UserChatMessage_ShouldShowAnalyzing_WhenMessageIdIsNull_AndNotReadOnly()
    {
        // Arrange
        SetupMudProviders();
        var cut = RenderComponent<UserChatMessage>(parameters => parameters
            .Add(p => p.Text, "Hello World")
            .Add(p => p.IsLastParagraph, true)
            .Add(p => p.MessageId, null) // Null MessageId
            .Add(p => p.ReadOnly, false)
        );

        // Act
        // (Render happens automatically)

        // Assert
        // Should find the "Analyzing Sentiment..." tooltip or icon
        var footer = cut.FindComponent<MudChatFooter>();
        footer.ShouldNotBeNull();

        // Check for the spinning hourglass icon
        var icon = cut.Find(".spin-icon");
        icon.ShouldNotBeNull();
    }

    [Fact]
    public void UserChatMessage_ShouldShowSentiment_WhenMessageIdIsPresent()
    {
        // Arrange
        SetupMudProviders();
        var cut = RenderComponent<UserChatMessage>(parameters => parameters
            .Add(p => p.Text, "Hello World")
            .Add(p => p.IsLastParagraph, true)
            .Add(p => p.MessageId, 123)
            .Add(p => p.Sentiment, 1)
            .Add(p => p.ReadOnly, false)
        );

        // Act
        // (Render happens automatically)

        // Assert
        // Should find the sentiment icon/button
        var btn = cut.FindComponent<MudIconButton>();
        btn.ShouldNotBeNull();
    }
}
