// Use polling instead of inotify to avoid watcher limits

using Microsoft.Extensions.Configuration;
using System.Reflection;

Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Read AI provider configuration from appsettings.json
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

// Parse TextGenerationModel configuration
string? textGenProviderStr = configuration["TextGenerationModel:Provider"] ?? "Ollama";
string? textGenEndpoint = configuration["TextGenerationModel:Endpoint"];
string? textGenName = configuration["TextGenerationModel:Name"] ?? "gemma3";
string? textGenAuthStr = configuration["TextGenerationModel:Auth"] ?? "None";
string? textGenKey = configuration["TextGenerationModel:Key"];
bool isTextGenOllama = string.Equals(textGenProviderStr, "Ollama", StringComparison.OrdinalIgnoreCase);

// Parse EmbeddingModel configuration
string? embedProviderStr = configuration["EmbeddingModel:Provider"] ?? "Ollama";
string? embedEndpoint = configuration["EmbeddingModel:Endpoint"];
string? embedName = configuration["EmbeddingModel:Name"] ?? "nomic-embed-text:v1.5";
string? embedAuthStr = configuration["EmbeddingModel:Auth"] ?? "None";
string? embedKey = configuration["EmbeddingModel:Key"];
bool isEmbedOllama = string.Equals(embedProviderStr, "Ollama", StringComparison.OrdinalIgnoreCase);

// Determine if we need Ollama container (only if Provider is Ollama and Endpoint is empty/external)
bool needsOllamaContainer = (isTextGenOllama && string.IsNullOrWhiteSpace(textGenEndpoint)) ||
                             (isEmbedOllama && string.IsNullOrWhiteSpace(embedEndpoint));

// Determine if using external Ollama (Endpoint is set and not empty)
bool usingExternalOllamaForTextGen = isTextGenOllama && !string.IsNullOrWhiteSpace(textGenEndpoint);
bool usingExternalOllamaForEmbed = isEmbedOllama && !string.IsNullOrWhiteSpace(embedEndpoint);

// We'll be consolidating our various datastores into PostgreSQL with JSONB and pgvector in the future,
IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17-trixie")
    .WithIconName("DatabaseSwitch")
    .WithDataVolume("jaimes-pg17-v4", false);

IResourceBuilder<PostgresServerResource> pgAdmin = postgres.WithPgAdmin(admin =>
{
    admin.WithIconName("TaskListSquareDatabase");
    admin.WithHostPort(5858);
    admin.WithParentRelationship(postgres);
    admin.WithUrls(u =>
    {
        u.Urls.Clear();
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:5858", DisplayText = "📋 pgAdmin" });
    });
});

IResourceBuilder<PostgresDatabaseResource> postgresdb = postgres.AddDatabase("postgres-db", "postgres")
    .WithCreationScript("CREATE EXTENSION IF NOT EXISTS vector;");

// Conditionally create Ollama container only if needed
IResourceBuilder<OllamaResource>? ollama = null;
IResourceBuilder<OllamaModelResource>? chatModel = null;
IResourceBuilder<OllamaModelResource>? embedModel = null;

if (needsOllamaContainer)
{
    ollama = builder.AddOllama("ollama-models")
        .WithIconName("BrainSparkle")
        .WithDataVolume();

    // Conditionally create models only if Ollama container exists
    if (isTextGenOllama && string.IsNullOrWhiteSpace(textGenEndpoint))
    {
        chatModel = ollama.AddModel("chatModel", textGenName).WithIconName("CommentText");
    }

    if (isEmbedOllama && string.IsNullOrWhiteSpace(embedEndpoint))
    {
        embedModel = ollama.AddModel("embedModel", embedName).WithIconName("CodeTextEdit");
    }
}
// Add Qdrant for vector embeddings
// Note: Qdrant API key is an Aspire parameter (not user secret) because it's required by the Aspire-managed Qdrant resource.
// Application-level secrets (e.g., Azure OpenAI API keys) are managed via user secrets.
IResourceBuilder<ParameterResource> qdrantApiKey = builder.AddParameter("qdrant-api-key", "qdrant", secret: true)
    .WithDescription("API key for Qdrant vector database");

IResourceBuilder<QdrantServerResource> qdrant = builder.AddQdrant("qdrant-embeddings", qdrantApiKey)
    .WithIconName("DatabaseSearch")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// Add LavinMQ for messaging (wire-compatible with RabbitMQ)
IResourceBuilder<LavinMQContainerResource> lavinmq = builder.AddLavinMQ("messaging")
    .WithIconName("DocumentQueue")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithUrls(u =>
    {
        u.Urls.Clear();
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:15672", DisplayText = "📋 Management" });
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:15672/queues", DisplayText = "📬 Queues" });
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:15672/consumers", DisplayText = "👥 Consumers" });
    });

// Note: MongoDB has been replaced with PostgreSQL + JSONB for document storage
// All document data (metadata, cracked documents, and chunks) are now stored in PostgreSQL

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("jaimes-api")
    .WithIconName("DocumentGlobe", IconVariant.Regular)
    .WithExternalHttpEndpoints()
    //.WithUrls(u => u.Urls.Clear())
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🌳 Root")
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/openapi/v1.json",
            DisplayText = "🌐 OpenAPI"
        })
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/swagger",
            DisplayText = "📃 Swagger"
        })
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/health",
            DisplayText = "👨‍⚕️ Health"
        });

// Conditionally add Ollama model references
if (chatModel != null)
{
    apiService = apiService.WithReference(chatModel);
}

if (embedModel != null)
{
    apiService = apiService.WithReference(embedModel);
}

apiService = apiService
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithReference(lavinmq)
    .WaitFor(qdrant)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(lavinmq);

// Conditionally wait for Ollama if container exists
if (ollama != null)
{
    apiService = apiService.WaitFor(ollama);
}

// Pass provider configuration via environment variables
apiService = apiService.WithEnvironment(context =>
{
    // TextGenerationModel configuration
    context.EnvironmentVariables["TextGenerationModel__Provider"] = textGenProviderStr;
    if (!string.IsNullOrWhiteSpace(textGenEndpoint))
    {
        context.EnvironmentVariables["TextGenerationModel__Endpoint"] = textGenEndpoint;
    }
    if (!string.IsNullOrWhiteSpace(textGenName))
    {
        context.EnvironmentVariables["TextGenerationModel__Name"] = textGenName;
    }
    context.EnvironmentVariables["TextGenerationModel__Auth"] = textGenAuthStr;
    if (!string.IsNullOrWhiteSpace(textGenKey))
    {
        context.EnvironmentVariables["TextGenerationModel__Key"] = textGenKey;
    }

    // EmbeddingModel configuration
    context.EnvironmentVariables["EmbeddingModel__Provider"] = embedProviderStr;
    if (!string.IsNullOrWhiteSpace(embedEndpoint))
    {
        context.EnvironmentVariables["EmbeddingModel__Endpoint"] = embedEndpoint;
    }
    if (!string.IsNullOrWhiteSpace(embedName))
    {
        context.EnvironmentVariables["EmbeddingModel__Name"] = embedName;
    }
    context.EnvironmentVariables["EmbeddingModel__Auth"] = embedAuthStr;
    if (!string.IsNullOrWhiteSpace(embedKey))
    {
        context.EnvironmentVariables["EmbeddingModel__Key"] = embedKey;
    }

    // If using Aspire-managed Ollama, set connection strings
    if (chatModel != null)
    {
        context.EnvironmentVariables["ConnectionStrings__chatModel"] = chatModel.Resource.ConnectionStringExpression;
    }
    else if (usingExternalOllamaForTextGen && !string.IsNullOrWhiteSpace(textGenEndpoint))
    {
        // For external Ollama, we don't set connection string, just endpoint
    }

    if (embedModel != null)
    {
        context.EnvironmentVariables["ConnectionStrings__embedModel"] = embedModel.Resource.ConnectionStringExpression;
    }
    else if (usingExternalOllamaForEmbed && !string.IsNullOrWhiteSpace(embedEndpoint))
    {
        // For external Ollama, we don't set connection string, just endpoint
    }
});

builder.AddProject<Projects.JAIMES_AF_Web>("jaimes-chat")
    .WithIconName("GameChat")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/health",
            DisplayText = "👨‍⚕️ Health"
        })
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🏠 Home")
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/games",
            DisplayText = "🎮 Games"
        })
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/admin",
            DisplayText = "⚙️ Admin"
        })
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/scenarios",
            DisplayText = "📖 Scenarios"
        })
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/players",
            DisplayText = "👤 Players"
        })
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentCrackerWorker>("document-cracker-worker")
    .WithIconName("DocumentTextExtract")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChangeDetector>("document-change-detector")
    .WithIconName("DocumentSearch")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb);

IResourceBuilder<ProjectResource> documentChunkingWorker = builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChunking>("document-chunking-worker")
    .WithIconName("DocumentSplit")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant);

// Conditionally add Ollama model reference
if (embedModel != null)
{
    documentChunkingWorker = documentChunkingWorker.WithReference(embedModel);
}

documentChunkingWorker = documentChunkingWorker
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant);

// Conditionally wait for Ollama if container exists
if (ollama != null)
{
    documentChunkingWorker = documentChunkingWorker.WaitFor(ollama);
}

documentChunkingWorker = documentChunkingWorker.WithEnvironment(context =>
{
    // Set Qdrant endpoint
    EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
    context.EnvironmentVariables["DocumentChunking__QdrantHost"] = qdrantGrpcEndpoint.Host;
    context.EnvironmentVariables["DocumentChunking__QdrantPort"] = qdrantGrpcEndpoint.Port;
    context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] =
        qdrant.Resource.ConnectionStringExpression;

    // Set Qdrant API key (use the parameter value)
    context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;

    // EmbeddingModel configuration
    context.EnvironmentVariables["EmbeddingModel__Provider"] = embedProviderStr;
    if (!string.IsNullOrWhiteSpace(embedEndpoint))
    {
        context.EnvironmentVariables["EmbeddingModel__Endpoint"] = embedEndpoint;
    }
    else if (ollama != null && embedModel != null)
    {
        // Set Ollama endpoint for embedding generation (needed for SemanticChunker) when using Aspire-managed Ollama
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["EmbeddingModel__Endpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
    }
    if (!string.IsNullOrWhiteSpace(embedName))
    {
        context.EnvironmentVariables["EmbeddingModel__Name"] = embedName;
    }
    context.EnvironmentVariables["EmbeddingModel__Auth"] = embedAuthStr;
    if (!string.IsNullOrWhiteSpace(embedKey))
    {
        context.EnvironmentVariables["EmbeddingModel__Key"] = embedKey;
    }

    // Legacy DocumentChunking__OllamaEndpoint for backward compatibility
    if (ollama != null && embedModel != null)
    {
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentChunking__OllamaEndpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
    }
    else if (!string.IsNullOrWhiteSpace(embedEndpoint))
    {
        context.EnvironmentVariables["DocumentChunking__OllamaEndpoint"] = embedEndpoint;
    }
});

IResourceBuilder<ProjectResource> documentEmbeddingWorker = builder.AddProject<Projects.JAIMES_AF_Workers_DocumentEmbedding>("document-embedding-worker")
    .WithIconName("DocumentEmbed")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant);

// Conditionally add Ollama model reference
if (embedModel != null)
{
    documentEmbeddingWorker = documentEmbeddingWorker.WithReference(embedModel);
}

documentEmbeddingWorker = documentEmbeddingWorker
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant);

// Conditionally wait for Ollama if container exists
if (ollama != null)
{
    documentEmbeddingWorker = documentEmbeddingWorker.WaitFor(ollama);
}

documentEmbeddingWorker = documentEmbeddingWorker.WithEnvironment(context =>
{
    // Set Qdrant endpoint
    EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
    context.EnvironmentVariables["DocumentEmbedding__QdrantHost"] = qdrantGrpcEndpoint.Host;
    context.EnvironmentVariables["DocumentEmbedding__QdrantPort"] = qdrantGrpcEndpoint.Port;
    context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] =
        qdrant.Resource.ConnectionStringExpression;

    // Set Qdrant API key (use the parameter value)
    context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;

    // EmbeddingModel configuration
    context.EnvironmentVariables["EmbeddingModel__Provider"] = embedProviderStr;
    if (!string.IsNullOrWhiteSpace(embedEndpoint))
    {
        context.EnvironmentVariables["EmbeddingModel__Endpoint"] = embedEndpoint;
    }
    else if (ollama != null && embedModel != null)
    {
        // Set Ollama endpoint for embedding generation when using Aspire-managed Ollama
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["EmbeddingModel__Endpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
    }
    if (!string.IsNullOrWhiteSpace(embedName))
    {
        context.EnvironmentVariables["EmbeddingModel__Name"] = embedName;
    }
    context.EnvironmentVariables["EmbeddingModel__Auth"] = embedAuthStr;
    if (!string.IsNullOrWhiteSpace(embedKey))
    {
        context.EnvironmentVariables["EmbeddingModel__Key"] = embedKey;
    }

    // Legacy DocumentEmbedding__OllamaEndpoint for backward compatibility
    if (ollama != null && embedModel != null)
    {
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentEmbedding__OllamaEndpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
    }
    else if (!string.IsNullOrWhiteSpace(embedEndpoint))
    {
        context.EnvironmentVariables["DocumentEmbedding__OllamaEndpoint"] = embedEndpoint;
    }
});

DistributedApplication app = builder.Build();

app.Run();