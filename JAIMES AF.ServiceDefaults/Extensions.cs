using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;

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
        builder.Services.Configure<ServiceDiscoveryOptions>(options => { options.AllowedSchemes = ["http", "https"]; });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Enable OpenTelemetry SDK self-diagnostics for troubleshooting export issues
        // The SDK supports file-based self-diagnostics via OTEL_DIAGNOSTICS_FILE environment variable
        // Create a otel-diagnostics.json file in the app directory with content like:
        // {"LogDirectory": ".", "FileSize": 32768, "LogLevel": "Error"}
        // Alternatively, set these environment variables for verbose logging:
        // - OTEL_LOG_LEVEL=debug  (for verbose OTLP exporter logging)
        // - OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf (we already default to this)

        // Log OTLP exporter configuration for debugging at startup
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var otelLogLevel = builder.Configuration["OTEL_LOG_LEVEL"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            Console.WriteLine($"[OTel-Startup] OTLP Endpoint: {otlpEndpoint}");
            Console.WriteLine($"[OTel-Startup] Protocol: HttpProtobuf (HTTP/1.1)");
            Console.WriteLine($"[OTel-Startup] Application: {builder.Environment.ApplicationName}");
            if (!string.IsNullOrWhiteSpace(otelLogLevel))
            {
                Console.WriteLine($"[OTel-Startup] Log Level: {otelLogLevel}");
            }

            Console.WriteLine("[OTel-Startup] Tip: Set OTEL_LOG_LEVEL=debug for verbose export diagnostics");
        }

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
                // Ensure we're sampling all activities - use AlwaysOn sampler to capture everything
                // This helps diagnose if the issue is with sampling or something else
                tracing.SetSampler(new AlwaysOnSampler());

                // Register activity sources first - prioritize application name for reliability
                // Use explicit registrations and wildcards to ensure we capture all activities
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(DefaultActivitySourceName)
                    // Add wildcard pattern for application name to catch all activities from this app
                    .AddSource($"{builder.Environment.ApplicationName}*")
                    // Explicit registrations for known ActivitySources
                    // Workers register their own sources in Program.cs, but include common patterns
                    .AddSource("Jaimes.DocumentCracker")
                    .AddSource("Jaimes.Workers.*")
                    .AddSource("Jaimes.Agents.*")
                    .AddSource("Jaimes.SentimentAnalysis") // Sentiment classification telemetry
                    // Azure SDK and OpenAI activity sources for gen_ai semantic conventions
                    .AddSource("OpenAI.*") // OpenAI SDK built-in tracing
                    .AddSource("Azure.AI.*") // Azure AI SDK tracing
                    // Agent framework sources (explicit patterns for reliability)
                    .AddSource("Microsoft.Extensions.AI")
                    .AddSource("Microsoft.Agents.AI");

                // Processor disabled - it's filtering out all traces
                // Need to investigate why the processor is too aggressive
                // tracing.AddProcessor(new BlazorActivityFilteringProcessor());

                // Add ASP.NET Core instrumentation with minimal filtering
                // Temporarily only filtering health checks to diagnose trace capture issues
                // Reference: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md
                tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context =>
                        {
                            // Exclude health check requests only
                            if (context.Request.Path.StartsWithSegments(HealthEndpointPath,
                                    StringComparison.OrdinalIgnoreCase) ||
                                context.Request.Path.StartsWithSegments(AlivenessEndpointPath,
                                    StringComparison.OrdinalIgnoreCase))
                                return false;

                            if (context.Request.Path.StartsWithSegments("/_blazor",
                                    StringComparison.OrdinalIgnoreCase))
                                return false;

                            // Allow all other requests for now (including Blazor/SignalR)
                            // We'll add back filtering once we confirm traces are working
                            return true;
                        };
                    })
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

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var useOtlpExporter = !string.IsNullOrWhiteSpace(otlpEndpoint);

        if (useOtlpExporter)
        {
            // Explicitly use HTTP/protobuf protocol for consistency
            // Aspire's OTLP receiver must also be configured for HTTP via DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL
            builder.Services.AddOpenTelemetry().UseOtlpExporter(
                OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf,
                new Uri(otlpEndpoint!));
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
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
            app.MapHealthChecks(AlivenessEndpointPath,
                new HealthCheckOptions
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
            if (args.Outcome.Result?.RequestMessage?.Method == HttpMethod.Post) return ValueTask.FromResult(false);

            // For non-POST requests or exceptions, use default retry logic
            return ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                (args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError &&
                 args.Outcome.Result?.StatusCode <= (System.Net.HttpStatusCode)599));
        };
    }
}