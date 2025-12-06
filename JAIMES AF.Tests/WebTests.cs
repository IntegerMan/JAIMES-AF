using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        if (!AspireTestsEnabled()) return;

        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.JAIMES_AF_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using DistributedApplication app =
            await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        HttpClient httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
        HttpResponseMessage response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static bool AspireTestsEnabled()
    {
        string? enabledValue = Environment.GetEnvironmentVariable("ENABLE_ASPIRE_TESTS");

        if (string.IsNullOrWhiteSpace(enabledValue)) return false;

        if (bool.TryParse(enabledValue, out bool parsedValue)) return parsedValue;

        return string.Equals(enabledValue, "1", StringComparison.OrdinalIgnoreCase);
    }
}