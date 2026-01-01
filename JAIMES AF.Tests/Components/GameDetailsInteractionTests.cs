using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using Moq.Protected;
using System.Net;

namespace MattEland.Jaimes.Tests.Components;

public class GameDetailsInteractionTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public GameDetailsInteractionTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IJSRuntime>(new Mock<IJSRuntime>().Object);
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

    [Fact]
    public async Task PressingEnter_ShouldClearChatTextbox()
    {
        // Arrange
        Guid gameId = Guid.NewGuid();

        // Mock HttpClientFactory
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        var gameResponse = new GameStateResponse
        {
            GameId = gameId,
            PlayerName = "TestPlayer",
            ScenarioName = "TestScenario",
            RulesetId = "rules-123",
            ScenarioId = "scenario-123",
            PlayerId = "player-123",
            RulesetName = "Ruleset Name",
            Messages = Array.Empty<MessageResponse>()
        };

        // Setup INITIAL LOAD call
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains($"/games/{gameId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(gameResponse),
            });

        // Setup AGUI CHAT call
        var agentResponse = new AgentRunResponse
        {
            Messages = new[] { new ChatMessage(ChatRole.Assistant, "Hello from Agent!") }
        };
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post && m.RequestUri!.PathAndQuery.Contains($"/chat")),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(async () =>
            {
                await Task.Delay(500); // Simulate network/AI delay
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(agentResponse),
                };
            });

        // Setup initial metadata call (if any)
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post && m.RequestUri!.PathAndQuery.Contains("/messages/metadata")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new MessagesMetadataResponse()),
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        Services.AddSingleton(clientFactoryMock.Object);

        SetupMudProviders();

        // Render component
        var cut = RenderComponent<GameDetails>(parameters => parameters.Add(p => p.GameId, gameId));

        // Wait for loading to finish
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Find the text field
        var textField = cut.FindComponent<MudTextField<string>>();

        // Act - Type a message
        var input = textField.Find("input");
        input.Input("Test Message");

        // Assert initial state
        textField.Instance.Value.ShouldBe("Test Message");

        // Act - Press Enter
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert - The value should be cleared
        cut.WaitForAssertion(() =>
        {
            var field = cut.FindComponent<MudTextField<string>>();
            field.Instance.Value.ShouldBe(string.Empty);
        });
    }
}
