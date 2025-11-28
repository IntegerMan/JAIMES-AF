using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Extension methods for configuring Qdrant client in dependency injection containers.
/// </summary>
public static class QdrantExtensions
{
    /// <summary>
    /// Resolved Qdrant connection configuration values.
    /// </summary>
    public record QdrantConnectionConfig
    {
        /// <summary>
        /// The Qdrant host address.
        /// </summary>
        public required string Host { get; init; }

        /// <summary>
        /// The Qdrant port number.
        /// </summary>
        public required int Port { get; init; }

        /// <summary>
        /// Whether to use HTTPS for the connection.
        /// </summary>
        public required bool UseHttps { get; init; }
    }

    /// <summary>
    /// Configuration options for Qdrant client setup.
    /// </summary>
    public class QdrantConfigurationOptions
    {
        /// <summary>
        /// The configuration section prefix to use (e.g., "DocumentEmbedding" or "DocumentChunking").
        /// Used to look up configuration like "{SectionPrefix}:QdrantHost".
        /// </summary>
        public string SectionPrefix { get; set; } = string.Empty;

        /// <summary>
        /// The connection string name to look up (defaults to "qdrant-embeddings").
        /// </summary>
        public string ConnectionStringName { get; set; } = "qdrant-embeddings";

        /// <summary>
        /// Whether to require host and port configuration (defaults to true).
        /// If false, will use localhost:6334 as fallback.
        /// </summary>
        public bool RequireConfiguration { get; set; } = true;

        /// <summary>
        /// Default API key to use if none is found (defaults to null, meaning no API key).
        /// Set to "qdrant" if you want to use the default Qdrant API key.
        /// </summary>
        public string? DefaultApiKey { get; set; }

        /// <summary>
        /// Additional configuration keys to check for API key (in addition to standard ones).
        /// </summary>
        public string[]? AdditionalApiKeyKeys { get; set; }
    }

    /// <summary>
    /// Configures and registers a Qdrant client in the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="options">Configuration options for Qdrant setup.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQdrantClient(
        this IServiceCollection services,
        IConfiguration configuration,
        QdrantConfigurationOptions? options = null)
    {
        AddQdrantClient(services, configuration, options, out _);
        return services;
    }

    /// <summary>
    /// Configures and registers a Qdrant client in the service collection and returns configuration values.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="options">Configuration options for Qdrant setup.</param>
    /// <param name="config">Output parameter containing the resolved Qdrant configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQdrantClient(
        this IServiceCollection services,
        IConfiguration configuration,
        QdrantConfigurationOptions? options,
        out QdrantConnectionConfig config)
    {
        options ??= new QdrantConfigurationOptions();

        // Get connection string
        string? qdrantConnectionString = configuration.GetConnectionString(options.ConnectionStringName);
        
        // Get host and port from configuration section
        string? qdrantHost = configuration[$"{options.SectionPrefix}:QdrantHost"];
        string? qdrantPortStr = configuration[$"{options.SectionPrefix}:QdrantPort"];
        string? qdrantApiKey = null;

        // Extract from connection string if provided (takes precedence)
        if (!string.IsNullOrWhiteSpace(qdrantConnectionString))
        {
            QdrantConnectionStringParser.ApplyQdrantConnectionString(
                qdrantConnectionString, 
                ref qdrantHost, 
                ref qdrantPortStr, 
                ref qdrantApiKey);
        }

        // Try to get API key from additional configuration sources if not found in connection string
        if (string.IsNullOrWhiteSpace(qdrantApiKey))
        {
            qdrantApiKey = GetApiKeyFromConfiguration(configuration, options);
        }

        // Validate and set defaults
        if (string.IsNullOrWhiteSpace(qdrantHost) || string.IsNullOrWhiteSpace(qdrantPortStr))
        {
            if (options.RequireConfiguration)
            {
                throw new InvalidOperationException(
                    $"Qdrant host and port are not configured. " +
                    $"Expected '{options.SectionPrefix}:QdrantHost' and '{options.SectionPrefix}:QdrantPort' from Aspire.");
            }
            
            // Use defaults if not required
            qdrantHost ??= "localhost";
            qdrantPortStr ??= "6334";
        }

        if (!int.TryParse(qdrantPortStr, out int qdrantPort))
        {
            throw new InvalidOperationException(
                $"Invalid Qdrant port: '{qdrantPortStr}'. Expected a valid integer.");
        }

        bool useHttps = configuration.GetValue<bool>($"{options.SectionPrefix}:QdrantUseHttps", defaultValue: false);

        // Create and register Qdrant client
        QdrantClient qdrantClient = string.IsNullOrWhiteSpace(qdrantApiKey)
            ? new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps)
            : new QdrantClient(qdrantHost, port: qdrantPort, https: useHttps, apiKey: qdrantApiKey);

        services.AddSingleton(qdrantClient);

        // Set output parameter with configuration values
        config = new QdrantConnectionConfig
        {
            Host = qdrantHost,
            Port = qdrantPort,
            UseHttps = useHttps
        };

        return services;
    }

    private static string? GetApiKeyFromConfiguration(IConfiguration configuration, QdrantConfigurationOptions options)
    {
        // Standard API key lookup locations
        string? apiKey = configuration["Qdrant__ApiKey"]
            ?? configuration["Qdrant:ApiKey"]
            ?? configuration["QDRANT_EMBEDDINGS_APIKEY"]
            ?? configuration["QdrantEmbeddings__ApiKey"]
            ?? configuration["QDRANT_EMBEDDINGS_API_KEY"]
            ?? configuration["Aspire:Resources:qdrant-embeddings:ApiKey"]
            ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY")
            ?? Environment.GetEnvironmentVariable("QdrantEmbeddings__ApiKey")
            ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_API_KEY")
            ?? Environment.GetEnvironmentVariable("qdrant-api-key");

        // Check additional keys if provided
        if (string.IsNullOrWhiteSpace(apiKey) && options.AdditionalApiKeyKeys != null)
        {
            foreach (string key in options.AdditionalApiKeyKeys)
            {
                apiKey = configuration[key] ?? Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    break;
                }
            }
        }

        // Handle unresolved Aspire expressions (containing {})
        if (!string.IsNullOrWhiteSpace(apiKey) && apiKey.Contains('{') && apiKey.Contains('}'))
        {
            string? resolvedApiKey = Environment.GetEnvironmentVariable("qdrant-api-key")
                ?? Environment.GetEnvironmentVariable("QDRANT_API_KEY")
                ?? Environment.GetEnvironmentVariable("Qdrant__ApiKey")
                ?? Environment.GetEnvironmentVariable("QDRANT_EMBEDDINGS_APIKEY");

            if (!string.IsNullOrWhiteSpace(resolvedApiKey) && !resolvedApiKey.Contains('{'))
            {
                apiKey = resolvedApiKey;
            }
            else if (options.DefaultApiKey != null)
            {
                apiKey = options.DefaultApiKey;
            }
        }

        // Use default if still not found
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = options.DefaultApiKey;
        }

        return apiKey;
    }

    /// <summary>
    /// Extracts Qdrant configuration values for logging purposes.
    /// Uses the same parsing logic as AddQdrantClient to ensure consistency.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="options">Configuration options for Qdrant setup.</param>
    /// <returns>The resolved Qdrant connection configuration.</returns>
    public static QdrantConnectionConfig GetQdrantConfiguration(
        IConfiguration configuration,
        QdrantConfigurationOptions options)
    {
        // Get connection string
        string? qdrantConnectionString = configuration.GetConnectionString(options.ConnectionStringName);
        
        // Get host and port from configuration section
        string? qdrantHost = configuration[$"{options.SectionPrefix}:QdrantHost"];
        string? qdrantPortStr = configuration[$"{options.SectionPrefix}:QdrantPort"];
        string? dummyApiKey = null;

        // Extract from connection string if provided (takes precedence)
        if (!string.IsNullOrWhiteSpace(qdrantConnectionString))
        {
            QdrantConnectionStringParser.ApplyQdrantConnectionString(
                qdrantConnectionString, 
                ref qdrantHost, 
                ref qdrantPortStr, 
                ref dummyApiKey);
        }

        // Set defaults
        qdrantHost ??= "localhost";
        qdrantPortStr ??= "6334";
        
        if (!int.TryParse(qdrantPortStr, out int qdrantPort))
        {
            qdrantPort = 6334; // Fallback to default port
        }

        bool useHttps = configuration.GetValue<bool>($"{options.SectionPrefix}:QdrantUseHttps", defaultValue: false);

        return new QdrantConnectionConfig
        {
            Host = qdrantHost,
            Port = qdrantPort,
            UseHttps = useHttps
        };
    }
}

