using Microsoft.Extensions.Configuration;
using System.Reflection;
using MattEland.Jaimes.AppHost;
using static MattEland.Jaimes.AppHost.AppHostHelpers;

// Use polling instead of inotify to avoid watcher limits
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
var textGenConfig = new ModelProviderConfig(
    Provider: configuration["TextGenerationModel:Provider"] ?? "Ollama",
    Endpoint: configuration["TextGenerationModel:Endpoint"],
    Name: configuration["TextGenerationModel:Name"] ?? "llama3.2:3b",
    Auth: configuration["TextGenerationModel:Auth"] ?? "None",
    Key: configuration["TextGenerationModel:Key"]);
bool isTextGenOllama = string.Equals(textGenConfig.Provider, "Ollama", StringComparison.OrdinalIgnoreCase);

// Parse EmbeddingModel configuration
var embedConfig = new ModelProviderConfig(
    Provider: configuration["EmbeddingModel:Provider"] ?? "Ollama",
    Endpoint: configuration["EmbeddingModel:Endpoint"],
    Name: configuration["EmbeddingModel:Name"] ?? "nomic-embed-text:v1.5",
    Auth: configuration["EmbeddingModel:Auth"] ?? "None",
    Key: configuration["EmbeddingModel:Key"]);
bool isEmbedOllama = string.Equals(embedConfig.Provider, "Ollama", StringComparison.OrdinalIgnoreCase);

// Determine if we need Ollama container (only if Provider is Ollama and Endpoint is empty)
bool needsOllamaContainer = (isTextGenOllama && string.IsNullOrWhiteSpace(textGenConfig.Endpoint)) ||
                            (isEmbedOllama && string.IsNullOrWhiteSpace(embedConfig.Endpoint));

// See https://storybooks.fluentui.dev/react/?path=/docs/icons-catalog--docs for available icons. Icon names should not end in "Regular", "Filled", etc.

// Conditionally create Ollama container only if needed
IResourceBuilder<OllamaResource>? ollama = null;
IResourceBuilder<OllamaModelResource>? chatModel = null;
IResourceBuilder<OllamaModelResource>? embedModel = null;

// ========================================
// ========================================
// APP GROUP (Ensures App is top of list)
// ========================================

IResourceBuilder<ContainerResource> appGroup = builder
    .AddContainer("app", "mcr.microsoft.com/dotnet/runtime-deps", "10.0")
    .WithIconName("Window")
    .WithEnvironment("DOTNET_RUNNING_IN_CONTAINER", "true")
    .ExcludeFromManifest();

// ========================================
// WEB APPLICATION
// ========================================

IResourceBuilder<ProjectResource> webApp = builder.AddProject<Projects.JAIMES_AF_Web>("jaimes-chat")
    .WithParentRelationship(appGroup)
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
            Url = "/admin",
            DisplayText = "⚙️ Admin"
        })
    .WithUrlForEndpoint("http",
        static _ => new ResourceUrlAnnotation
        {
            Url = "/games",
            DisplayText = "🎮 Games"
        });

// ========================================
// API SERVICE (Second in UI)
// ========================================

// Note: MongoDB has been replaced with PostgreSQL + JSONB for document storage
// All document data (metadata, cracked documents, and chunks) are now stored in PostgreSQL

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("jaimes-api")
    .WithParentRelationship(appGroup)
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

// ========================================
// INFRASTRUCTURE
// ========================================

// Create a parent group for all data resources
IResourceBuilder<ContainerResource> dataGroup = builder
    .AddContainer("data", "mcr.microsoft.com/dotnet/runtime-deps", "10.0")
    .WithIconName("Database")
    .WithEnvironment("DOTNET_RUNNING_IN_CONTAINER", "true")
    .ExcludeFromManifest();

if (needsOllamaContainer)
{
    ollama = builder.AddOllama("ollama-models")
        .WithIconName("BrainSparkle")
        .WithDataVolume()
        .WithParentRelationship(dataGroup);

    // Conditionally create models only if Ollama container exists
    if (isTextGenOllama && string.IsNullOrWhiteSpace(textGenConfig.Endpoint))
    {
        chatModel = ollama.AddModel("chatModel", textGenConfig.Name!).WithIconName("CommentText");
    }

    if (isEmbedOllama && string.IsNullOrWhiteSpace(embedConfig.Endpoint))
    {
        embedModel = ollama.AddModel("embedModel", embedConfig.Name!).WithIconName("CodeTextEdit");
    }
}

// We'll be consolidating our various datastores into PostgreSQL with JSONB and pgvector in the future
IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17-trixie")
    .WithIconName("DatabaseSwitch")
    .WithDataVolume("jaimes-pg17-v7", false)
    .WithParentRelationship(dataGroup);

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

// Add Qdrant for vector embeddings
// Note: Qdrant API key is an Aspire parameter (not user secret) because it's required by the Aspire-managed Qdrant resource.
// Application-level secrets (e.g., Azure OpenAI API keys) are managed via user secrets.
IResourceBuilder<ParameterResource> qdrantApiKey = builder.AddParameter("qdrant-api-key", "qdrant", secret: true)
    .WithDescription("API key for Qdrant vector database");

IResourceBuilder<QdrantServerResource> qdrant = builder.AddQdrant("qdrant-embeddings", qdrantApiKey)
    .WithIconName("DatabaseSearch")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithParentRelationship(dataGroup);

// Add LavinMQ for messaging (wire-compatible with RabbitMQ)
IResourceBuilder<LavinMQContainerResource> lavinmq = builder.AddLavinMQ("LavinMQ-Messaging")
    .WithIconName("PeopleQueue")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithUrls(u =>
    {
        u.Urls.Clear();
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:15672", DisplayText = "📋 Management" });
        u.Urls.Add(new ResourceUrlAnnotation { Url = "http://localhost:15672/queues", DisplayText = "📬 Queues" });
        u.Urls.Add(new ResourceUrlAnnotation
            { Url = "http://localhost:15672/consumers", DisplayText = "👥 Consumers" });
    })
    .WithParentRelationship(dataGroup);

// Group all workers under a parent resource for better UI organization
IResourceBuilder<ContainerResource> workersGroup = builder
    .AddContainer("workers", "mcr.microsoft.com/dotnet/runtime-deps", "10.0")
    .WithIconName("PeopleTeam")
    .WithEnvironment("DOTNET_RUNNING_IN_CONTAINER", "true")
    .ExcludeFromManifest();

// Migration worker must run first and complete before other services start
IResourceBuilder<ProjectResource> databaseMigrationWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_DatabaseMigration>("database-migration-worker")
    .WithIconName("DatabasePlugConnected")
    .WithReference(postgresdb)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WithParentRelationship(workersGroup);


apiService = apiService
    .WithOllamaReferences(ollama, chatModel, embedModel, needsChatModel: true, needsEmbedModel: true)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithReference(lavinmq)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(qdrant)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(lavinmq)
    .WithEnvironment(context =>
    {
        void SetVar(string key, object value) => context.EnvironmentVariables[key] = value;

        SetModelProviderEnvironmentVariables(SetVar,
            "TextGenerationModel",
            textGenConfig,
            chatModel,
            ollama,
            isTextGenOllama);
        SetModelProviderEnvironmentVariables(SetVar, "EmbeddingModel", embedConfig, embedModel, ollama, isEmbedOllama);
    });

webApp = webApp
    .WithReference(apiService)
    .WaitFor(apiService);

// Document processing configuration parameters
// UploadDocumentsWhenCracking: When true, PDF binary content is stored for viewing in admin UI
bool uploadDocumentsWhenCracking = bool.TryParse(
                                       configuration["DocumentProcessing:UploadDocumentsWhenCracking"],
                                       out bool parsed) && parsed
                                   || !configuration.GetSection("DocumentProcessing:UploadDocumentsWhenCracking")
                                       .Exists(); // Default to true

IResourceBuilder<ProjectResource> documentCrackerWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_DocumentCrackerWorker>("document-cracker-worker")
    .WithIconName("DocumentTextExtract")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WithEnvironment("DocumentCrackerWorker__UploadDocumentsWhenCracking", uploadDocumentsWhenCracking.ToString())
    .WithParentRelationship(workersGroup);

IResourceBuilder<ProjectResource> documentChangeDetector = builder
    .AddProject<Projects.JAIMES_AF_Workers_DocumentChangeDetector>("document-change-detector")
    .WithIconName("DocumentSearch")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WithEnvironment("DocumentChangeDetector__UploadDocumentsWhenCracking", uploadDocumentsWhenCracking.ToString())
    .WithParentRelationship(workersGroup);

IResourceBuilder<ProjectResource> documentChunkingWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_DocumentChunking>("document-chunking-worker")
    .WithIconName("DocumentPageBreak")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithOllamaReferences(ollama, chatModel, embedModel, needsChatModel: false, needsEmbedModel: true)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant)
    .WithParentRelationship(workersGroup)
    .WithEnvironment(context =>
    {
        void SetVar(string key, object value) => context.EnvironmentVariables[key] = value;

        SetQdrantEnvironmentVariables(SetVar, "DocumentChunking", qdrant, qdrantApiKey);
        SetModelProviderEnvironmentVariables(SetVar, "EmbeddingModel", embedConfig, embedModel, ollama, isEmbedOllama);
        SetLegacyOllamaEndpoint(SetVar, "DocumentChunking__OllamaEndpoint", ollama, embedModel, embedConfig.Endpoint);
    });

IResourceBuilder<ProjectResource> documentEmbeddingWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_DocumentEmbedding>("document-embedding-worker")
    .WithIconName("DocumentBulletListMultiple")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithOllamaReferences(ollama, chatModel, embedModel, needsChatModel: false, needsEmbedModel: true)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant)
    .WithParentRelationship(workersGroup)
    .WithEnvironment(context =>
    {
        void SetVar(string key, object value) => context.EnvironmentVariables[key] = value;

        SetQdrantEnvironmentVariables(SetVar, "DocumentEmbedding", qdrant, qdrantApiKey);
        SetModelProviderEnvironmentVariables(SetVar, "EmbeddingModel", embedConfig, embedModel, ollama, isEmbedOllama);
        SetLegacyOllamaEndpoint(SetVar, "DocumentEmbedding__OllamaEndpoint", ollama, embedModel, embedConfig.Endpoint);
    });

// Add parameter for user-message-worker replicas (configurable parallelism)
IResourceBuilder<ParameterResource> userWorkerReplicas = builder.AddParameter("user-worker-replicas", "1")
    .WithDescription("Number of user-message-worker replicas for parallel message processing");

IResourceBuilder<ProjectResource> userMessageWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_UserMessageWorker>("user-message-worker")
    .WithIconName("PersonChat")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(apiService) // For SignalR notification HTTP calls
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WithParentRelationship(workersGroup)
    .WithReplicas(int.TryParse(configuration["Parameters:user-worker-replicas"], out int userReplicas) ? userReplicas : 1);

// Add parameter for assistant-message-worker replicas (configurable parallelism)
IResourceBuilder<ParameterResource> assistantWorkerReplicas = builder.AddParameter("assistant-worker-replicas", "1")
    .WithDescription("Number of assistant-message-worker replicas for parallel message evaluation");

// Configure assistant worker replicas for parallel message processing
int assistantWorkerReplicaCount = int.TryParse(
    builder.Configuration["Parameters:assistant-worker-replicas"] ?? "1",
    out int replicas)
    ? replicas
    : 1;
IResourceBuilder<ProjectResource> assistantMessageWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_AssistantMessageWorker>("assistant-message-worker")
    .WithIconName("SettingsChat")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(apiService) // For SignalR notification HTTP calls
    .WithOllamaReferences(ollama, chatModel, embedModel, needsChatModel: true, needsEmbedModel: false)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WithParentRelationship(workersGroup)
    .WithReplicas(assistantWorkerReplicaCount)
    .WithEnvironment(context =>
    {
        void SetVar(string key, object value) => context.EnvironmentVariables[key] = value;

        SetModelProviderEnvironmentVariables(SetVar,
            "TextGenerationModel",
            textGenConfig,
            chatModel,
            ollama,
            isTextGenOllama);
    });

IResourceBuilder<ProjectResource> conversationEmbeddingWorker = builder
    .AddProject<Projects.JAIMES_AF_Workers_ConversationEmbeddingWorker>("conversation-embedding-worker")
    .WithIconName("ChatBubblesQuestion")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithOllamaReferences(ollama, chatModel, embedModel, needsChatModel: false, needsEmbedModel: true)
    .WaitFor(databaseMigrationWorker)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant)
    .WithParentRelationship(workersGroup)
    .WithEnvironment(context =>
    {
        void SetVar(string key, object value) => context.EnvironmentVariables[key] = value;

        SetQdrantEnvironmentVariables(SetVar, "ConversationEmbedding", qdrant, qdrantApiKey);
        SetModelProviderEnvironmentVariables(SetVar, "EmbeddingModel", embedConfig, embedModel, ollama, isEmbedOllama);
    });

DistributedApplication app = builder.Build();

app.Run();