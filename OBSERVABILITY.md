# Observability Configuration

This document describes the OpenTelemetry (OTEL) observability setup for the JAIMES AF solution.

## Overview

All services in the solution use OpenTelemetry to export traces, metrics, and structured logs to the Aspire dashboard. The configuration is centralized in `JAIMES AF.ServiceDefaults/Extensions.cs` and `JAIMES AF.AppHost/AppHostHelpers.cs`.

## Instrumentation Packages

The solution extends the standard Aspire observability with several OpenTelemetry instrumentation packages:

### Core Instrumentation

| Package | Purpose |
|---------|---------|
| `OpenTelemetry.Instrumentation.AspNetCore` | HTTP request/response tracing for APIs and Blazor |
| `OpenTelemetry.Instrumentation.Http` | Outgoing HTTP client call tracing |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics (GC, thread pool, etc.) |

### Database & Storage

| Package | Purpose |
|---------|---------|
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | EF Core query tracing with SQL details |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | Redis operations tracing (Qdrant cache) |

### AI & Agent Framework

| Activity Source | Purpose |
|-----------------|---------|
| `Microsoft.Extensions.AI` | Microsoft.Extensions.AI chat client operations |
| `Microsoft.Agents.AI` | Microsoft Agent Framework activities |
| `OpenAI.*` | OpenAI SDK built-in distributed tracing |
| `Azure.AI.*` | Azure AI SDK tracing (Azure OpenAI) |

### Custom Application Sources

| Activity Source | Purpose |
|-----------------|---------|
| `Jaimes.Agents.*` | Custom agent and tool invocation tracing |
| `Jaimes.Workers.*` | Background worker pipeline tracing |
| `Jaimes.SentimentAnalysis` | ML.NET sentiment classification operations |
| `Jaimes.DocumentCracker` | Document processing pipeline tracing |

### Metrics

Custom meters for detailed AI performance tracking:
- `Jaimes.Agents.ChatClient` - Chat completion latencies and token counts
- `Jaimes.Agents.Run` - Agent run durations and success rates
- `Jaimes.Agents.Tools` - Tool invocation counts and durations

## Protocol Configuration

The solution uses **HTTP/Protobuf** protocol for OTLP export instead of the default gRPC. This is configured in:

- **`launchSettings.json`**: Sets `ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` to enable the HTTP listener on the Aspire dashboard
- **`Extensions.cs`**: Explicitly configures the OTLP exporter to use `HttpProtobuf` protocol
- **`AppHost.cs`**: Passes `OTEL_EXPORTER_OTLP_ENDPOINT` to all services

### Why HTTP/Protobuf?

HTTP/Protobuf was chosen over gRPC because:
- More consistent behavior on Windows (avoids HTTP/2 vs HTTP/1.1 issues)
- Simpler debugging (standard HTTP requests)
- Compatible with all hosting environments

## Aspire Parameters

### `otel-log-level`

Controls OpenTelemetry SDK diagnostic logging verbosity.

| Value | Description |
|-------|-------------|
| (empty) | Default minimal logging |
| `warning` | Warnings and errors only |
| `info` | Informational messages |
| `debug` | Verbose export diagnostics |
| `trace` | Most verbose SDK internals |

**Example `appsettings.json`:**
```json
{
  "Parameters": {
    "otel-log-level": "debug"
  }
}
```

### `enable-sensitive-logging`

When enabled, AI prompts and responses are included in telemetry traces.

| Value | Description |
|-------|-------------|
| `false` (default) | Sensitive data excluded |
| `true` | Prompts/responses visible in traces |

> [!WARNING]
> Enable sensitive logging only in development. Never enable in production.

## Short-Lived Worker Requirements

For workers that exit quickly (like `database-migration-worker`), you **must** call `host.StopAsync()` before exiting to ensure telemetry is flushed:

```csharp
try
{
    // ... worker logic ...
}
finally
{
    await host.StopAsync();
}
```

Without this, the OTLP exporter may not have time to send batched telemetry before the process terminates.

## Startup Diagnostics

Each service logs its OTLP configuration at startup:

```
[OTel-Startup] OTLP Endpoint: http://localhost:19288
[OTel-Startup] Protocol: HttpProtobuf (HTTP/1.1)
[OTel-Startup] Application: database-migration-worker
[OTel-Startup] Log Level: debug
```

## Helper Methods

`AppHostHelpers.cs` provides centralized OTEL configuration:

- **`WithOtelConfiguration()`** - Extension method for services that only need OTEL config
- **`SetOtelEnvironmentVariables()`** - For services with other environment variables

## Troubleshooting

### No traces/logs in Aspire dashboard

1. Check startup logs for `[OTel-Startup]` messages
2. Set `otel-log-level` to `debug` to see export attempts
3. Look for HTTP 400/500 errors indicating protocol mismatch
4. Ensure short-lived workers call `host.StopAsync()`

### HTTP 400 errors on export

Protocol mismatch between client and server. Verify:
- `ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` is set in `launchSettings.json`
- `OTEL_EXPORTER_OTLP_ENDPOINT` points to the HTTP endpoint
