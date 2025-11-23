using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MattEland.Jaimes.ServiceDefaults;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Ensure a stable ActivitySource name that other parts of the app can use to create activities
    private const string DefaultActivitySourceName = "Jaimes.ApiService";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default, but exclude POST requests from retries
            http.AddStandardResilienceHandler(ConfigureResilienceHandlerExcludingPost);

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Allow both HTTP and HTTPS schemes for service discovery to support local development
        builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        {
            options.AllowedSchemes = ["http", "https"];
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Add meters for Agent Framework and Microsoft.Extensions.AI
                    // Using wildcards to capture all meters from these namespaces
                    .AddMeter("Microsoft.Agents.AI")
                    .AddMeter("Microsoft.Extensions.AI")
                    .AddMeter("Microsoft.Extensions.AI.*")
                    .AddMeter("Microsoft.Agents.AI.*")
                    // Also try the genai instrumentation meter if available
                    .AddMeter("genai")
                    // Add our custom meters for explicit metric tracking
                    .AddMeter("Jaimes.Agents.ChatClient")
                    .AddMeter("Jaimes.Agents.Run")
                    .AddMeter("Jaimes.Agents.Tools")
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                // Register the default activity source(s) so activities created elsewhere are captured.
                // Include the application name, an explicit default name, and the agent-framework source.
                tracing
                    .AddSource(
                        builder.Environment.ApplicationName,
                        DefaultActivitySourceName,
                        "*Microsoft.Extensions.AI",
                        "*Microsoft.Extensions.Agents*",
                        "agent-framework-source",
                        "MattEland.*",
                        "Jaimes.*",
                        // Explicitly add known ActivitySources to ensure they're registered
                        // (wildcards should match, but explicit registration is more reliable)
                        "Jaimes.DocumentCracker",
                        "Jaimes.Indexer")
                    .AddAspNetCoreInstrumentation(options =>
                        // Exclude health check requests from tracing
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            string stateDisplayName = $"{command.CommandType}";
                            activity.DisplayName = stateDisplayName;
                            activity.SetTag("db.name", stateDisplayName);
                        };
                    })
                    // Add Redis instrumentation for Kernel Memory operations
                    // This will capture Redis activities for debugging in Aspire
                    .AddRedisInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Check both configuration and environment variables for OTLP endpoint
        // Aspire sets this as an environment variable when services are added via AddProject
        string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] 
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }

    /// <summary>
    /// Configures a StandardResilienceHandler to exclude POST requests from retries.
    /// This prevents duplicate submissions and other side effects from retrying non-idempotent operations.
    /// Also configures a longer timeout for operations that may take significant time (e.g., document listing).
    /// </summary>
    public static void ConfigureResilienceHandlerExcludingPost(HttpStandardResilienceOptions options)
    {
        // Configure longer per-attempt and total request timeouts for operations that may take significant time
        TimeSpan extendedTimeout = TimeSpan.FromMinutes(5);
        options.AttemptTimeout.Timeout = extendedTimeout;
        options.TotalRequestTimeout.Timeout = extendedTimeout;

        // Ensure the circuit breaker sampling window is at least double the attempt timeout per Polly requirements
        options.CircuitBreaker.SamplingDuration = extendedTimeout + extendedTimeout;
        
        options.Retry.ShouldHandle = args =>
        {
            // Exclude POST requests from retries (only when we have a response)
            if (args.Outcome.Result?.RequestMessage?.Method == HttpMethod.Post)
            {
                return ValueTask.FromResult(false);
            }
            
            // For non-POST requests or exceptions, use default retry logic
            return ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                 args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                 (args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError &&
                  args.Outcome.Result?.StatusCode <= (System.Net.HttpStatusCode)599)));
        };
    }
}
