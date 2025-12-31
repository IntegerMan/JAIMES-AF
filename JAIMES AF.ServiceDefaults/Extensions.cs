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
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter;

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
        // Enable unencrypted HTTP/2 support to allow gRPC to talk to the Aspire Dashboard's OTLP endpoint (H2C)
        // This is crucial for local development where TLS is not used between services and the dashboard.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // Initialize internal diagnostic logger to debug OTLP export failures
        if (_otelListener == null)
        {
            _otelListener = new OpenTelemetryConsoleLogger();
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
                    .AddMeter("Microsoft.EntityFrameworkCore")
                    .AddMeter("Npgsql")
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
                    // Agent framework sources (explicit patterns for reliability)
                    .AddSource("Microsoft.Extensions.AI")
                    .AddSource("Microsoft.Agents.AI");

                // Add ASP.NET Core instrumentation with minimal filtering
                // Temporarily only filtering health checks to diagnose trace capture issues
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
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // OpenTelemetry standard configuration usually gives Environment Variables precedence over appsettings.json.
        // If Aspire injects 'grpc' into the environment, that will win unless we explicitly handle it.
        var otlpProtocol = builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];

        Console.WriteLine($"[Telemetry] Resolved Configuration:");
        Console.WriteLine($"  - OTEL_EXPORTER_OTLP_ENDPOINT: {otlpEndpoint ?? "(not set)"}");
        Console.WriteLine($"  - OTEL_EXPORTER_OTLP_PROTOCOL: {otlpProtocol ?? "(not set)"}");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            // Default to Grpc as requested.
            var protocol = OtlpExportProtocol.Grpc;

            if (!string.IsNullOrWhiteSpace(otlpProtocol))
            {
                if (otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                {
                    protocol = OtlpExportProtocol.Grpc;
                }
                else if (otlpProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase))
                {
                    protocol = OtlpExportProtocol.HttpProtobuf;
                }
                else
                {
                    // Error level validation as requested
                    Console.WriteLine(
                        $"[Telemetry] ERROR: Unrecognized OTLP protocol '{otlpProtocol}'. Defaulting to {protocol}. " +
                        "Supported values: 'grpc', 'http/protobuf'.");
                }
            }

            Console.WriteLine($"[Telemetry] Final Protocol Choice: {protocol}");
            builder.Services.AddOpenTelemetry().UseOtlpExporter(protocol, new Uri(otlpEndpoint));
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
                 args.Outcome.Result?.StatusCode <= (System.Net.HttpStatusCode) 599));
        };
    }

    /// <summary>
    /// EventListener that listens for OpenTelemetry internal diagnostics and prints them to the Console.
    /// This helps debug silent failures like OTLP export issues.
    /// </summary>
    private class OpenTelemetryConsoleLogger : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);

            if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            {
                // Enable verbose logging for everything OpenTelemetry
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            base.OnEventWritten(eventData);

            string message;
            if (eventData.Message != null && eventData.Payload != null)
            {
                message = string.Format(eventData.Message, eventData.Payload.ToArray());
            }
            else
            {
                message = eventData.Message ?? "Unknown OpenTelemetry Event";
            }

            Console.WriteLine(
                $"[OpenTelemetry Diagnostics] [{eventData.Level}] {eventData.EventSource.Name}: {message}");
        }
    }

    private static OpenTelemetryConsoleLogger? _otelListener;
}