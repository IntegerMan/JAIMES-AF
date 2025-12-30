using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

namespace MattEland.Jaimes.AppHost;

/// <summary>
/// Helper methods for configuring AI provider resources in Aspire AppHost.
/// </summary>
internal static class AppHostHelpers
{
    /// <summary>
    /// Configuration for a model provider (TextGenerationModel or EmbeddingModel).
    /// </summary>
    internal record ModelProviderConfig(
        string Provider,
        string? Endpoint,
        string? Name,
        string Auth,
        string? Key);

    /// <summary>
    /// Conditionally adds Ollama model references and waits for Ollama container only if references were actually added.
    /// </summary>
    internal static IResourceBuilder<ProjectResource> WithOllamaReferences(
        this IResourceBuilder<ProjectResource> resource,
        IResourceBuilder<OllamaResource>? ollama,
        IResourceBuilder<OllamaModelResource>? chatModel,
        IResourceBuilder<OllamaModelResource>? embedModel,
        bool needsChatModel = false,
        bool needsEmbedModel = false)
    {
        if (needsChatModel && chatModel != null)
        {
            resource = resource.WithReference(chatModel);
        }

        if (needsEmbedModel && embedModel != null)
        {
            resource = resource.WithReference(embedModel);
        }

        if (ollama != null && ((needsChatModel && chatModel != null) || (needsEmbedModel && embedModel != null)))
        {
            resource = resource.WaitFor(ollama);
        }

        return resource;
    }

    /// <summary>
    /// Sets environment variables for a model provider configuration.
    /// </summary>
    internal static void SetModelProviderEnvironmentVariables(
        Action<string, object> setVariable,
        string sectionPrefix,
        ModelProviderConfig config,
        IResourceBuilder<OllamaModelResource>? modelResource,
        IResourceBuilder<OllamaResource>? ollamaResource,
        bool isOllamaProvider)
    {
        setVariable($"{sectionPrefix}__Provider", config.Provider);
        setVariable($"{sectionPrefix}__Auth", config.Auth);

        if (!string.IsNullOrWhiteSpace(config.Endpoint))
        {
            setVariable($"{sectionPrefix}__Endpoint", config.Endpoint);
        }
        else if (isOllamaProvider && ollamaResource != null && modelResource != null)
        {
            // Set Ollama endpoint when using Aspire-managed Ollama
            var ollamaEndpoint = ollamaResource.GetEndpoint("http");
            setVariable($"{sectionPrefix}__Endpoint", $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}");
        }

        if (!string.IsNullOrWhiteSpace(config.Name))
        {
            setVariable($"{sectionPrefix}__Name", config.Name);
        }

        if (!string.IsNullOrWhiteSpace(config.Key))
        {
            setVariable($"{sectionPrefix}__Key", config.Key);
        }

        // Set connection string if using Aspire-managed Ollama
        if (modelResource != null)
        {
            var connectionStringName = sectionPrefix == "TextGenerationModel" ? "chatModel" : "embedModel";
            setVariable($"ConnectionStrings__{connectionStringName}",
                modelResource.Resource.ConnectionStringExpression);
        }
    }

    /// <summary>
    /// Sets Qdrant environment variables for a worker service.
    /// </summary>
    internal static void SetQdrantEnvironmentVariables(
        Action<string, object> setVariable,
        string sectionPrefix,
        IResourceBuilder<QdrantServerResource> qdrant,
        IResourceBuilder<ParameterResource> qdrantApiKey)
    {
        var qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        setVariable($"{sectionPrefix}__QdrantHost", qdrantGrpcEndpoint.Host);
        setVariable($"{sectionPrefix}__QdrantPort", qdrantGrpcEndpoint.Port);
        setVariable("ConnectionStrings__qdrant-embeddings", qdrant.Resource.ConnectionStringExpression);
        setVariable("qdrant-api-key", qdrantApiKey.Resource.ValueExpression);
    }

    /// <summary>
    /// Sets legacy Ollama endpoint environment variable for backward compatibility.
    /// </summary>
    internal static void SetLegacyOllamaEndpoint(
        Action<string, object> setVariable,
        string legacyKey,
        IResourceBuilder<OllamaResource>? ollama,
        IResourceBuilder<OllamaModelResource>? model,
        string? externalEndpoint)
    {
        if (ollama != null && model != null)
        {
            var ollamaEndpoint = ollama.GetEndpoint("http");
            setVariable(legacyKey, $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}");
        }
        else if (!string.IsNullOrWhiteSpace(externalEndpoint))
        {
            setVariable(legacyKey, externalEndpoint);
        }
    }

    /// <summary>
    /// Configures the OTLP exporter endpoint for a resource to ensure telemetry is collected.
    /// </summary>
    internal static IResourceBuilder<T> WithTelemetryEndpoint<T>(
        this IResourceBuilder<T> resource,
        IConfiguration configuration) where T : IResourceWithEnvironment
    {
        // Try to get the endpoint from the specific environment variable first (if set explicitly)
        // This takes precedence.
        string? otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        // If not set, try the Aspire Dashboard URL env var (which is set by launchSettings.json)
        // This is the common case for local development.
        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otlpEndpoint = configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];
        }

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            resource.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint);
        }

        return resource;
    }
}
