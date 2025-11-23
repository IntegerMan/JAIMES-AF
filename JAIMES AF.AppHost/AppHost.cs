// Use polling instead of inotify to avoid watcher limits
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

// Force Aspire to use Podman instead of Docker
Environment.SetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME", "podman", EnvironmentVariableTarget.Process);

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Add Ollama with nomic-embed-text model for embeddings
IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama-models")
    .WithIconName("BrainSparkle")
    //.WithContainerRuntimeArgs("--device", "nvidia.com/gpu=all") // Podman GPU config
    .WithDataVolume();
    /*
    .WithOpenWebUI(webUi => {
        webUi.WithIconName("ChatSparkle");
        webUi.WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/models",
            DisplayText = "🤖 Models"
        });
    }, "open-webUI");
    */

var embedModel = ollama.AddModel("nomic-embed-text").WithIconName("CodeTextEdit");
var chatModel = ollama.AddModel("gemma3").WithIconName("CommentText");

// NOTE: There is an Aspire integration for Redis, but it doesn't support Redis-Stack. If you customize the image, it still doesn't start Redis-Stack afterwards.
// It's simpler just to use a known good image with good default behavior.
IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis-embeddings", "redis/redis-stack:latest")
    .WithIconName("DatabaseLink")
    .WithHttpEndpoint(8001, 8001, name: "redisinsight")
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🔬 RedisInsight")
    .WithEndpoint(6379, 6379, name: "redis")
    .WithBindMount(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspire",
            "jaimes-redis-data"), "/data");

// Add Qdrant for vector embeddings
// Note: Qdrant API key is an Aspire parameter (not user secret) because it's required by the Aspire-managed Qdrant resource.
// Application-level secrets (e.g., Azure OpenAI API keys) are managed via user secrets.
var qdrantApiKey = builder.AddParameter("qdrant-api-key", "qdrant", secret: true)
    .WithDescription("API key for Qdrant vector database");

IResourceBuilder<QdrantServerResource> qdrant = builder.AddQdrant("qdrant-embeddings", qdrantApiKey)
    .WithIconName("DatabaseSearch")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

// Add SQLite database
IResourceBuilder<IResourceWithConnectionString> sqliteDb = builder.AddSqlite("jaimes-db")
    .WithIconName("WindowDatabase")
    .WithSqliteWeb(web => web.WithIconName("DatabaseSearch"));

// Add MongoDB for document storage
IResourceBuilder<MongoDBServerResource> mongo = builder.AddMongoDB("mongo")
    .WithIconName("BookDatabase")
    .WithMongoExpress(exp => exp.WithIconName("DocumentAdd"))
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

IResourceBuilder<MongoDBDatabaseResource> mongoDb = mongo.AddDatabase("documents")
    .WithIconName("DocumentData");

// Add RabbitMQ for messaging
var username = builder.AddParameter("rabbit-username", "guest", secret: false);
var password = builder.AddParameter("rabbit-password", "guest", secret: true);
IResourceBuilder<RabbitMQServerResource> rabbitmq = builder.AddRabbitMQ("messaging", username, password)
    .WithIconName("AnimalRabbit")
    .WithManagementPlugin();

// Add Seq for advanced log monitoring and analysis
IResourceBuilder<SeqResource> seq = builder.AddSeq("seq")
    .WithIconName("DeveloperBoardSearch")
    .ExcludeFromManifest()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("SEQ_LISTEN_URI", "http://0.0.0.0:80")
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🔍 Seq");

// Add Kernel Memory service container
IResourceBuilder<ContainerResource> kernelMemory = builder.AddContainer("kernel-memory", "kernelmemory/service:latest")
    .WithIconName("BrainCircuit")
    .WithHttpEndpoint(9001, 9001, name: "http")
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🧠 Kernel Memory")
    .WaitFor(ollama)
    .WaitFor(redis)
    .WithEnvironment(context =>
    {
        // Set environment to Production (required by Kernel Memory service)
        context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
        
        // Configure Ollama endpoint and models (shared configuration)
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        string ollamaUrl = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        context.EnvironmentVariables["KernelMemory__Services__Ollama__Endpoint"] = ollamaUrl;
        
        // Configure text generation (required by service)
        context.EnvironmentVariables["KernelMemory__TextGeneratorType"] = "Ollama";
        context.EnvironmentVariables["KernelMemory__Services__Ollama__TextModel"] = "gemma3";
        
        // Configure embedding generation
        context.EnvironmentVariables["KernelMemory__DataIngestion__EmbeddingGeneratorTypes__0"] = "Ollama";
        context.EnvironmentVariables["KernelMemory__Services__Ollama__EmbeddingModel"] = "nomic-embed-text";
        context.EnvironmentVariables["KernelMemory__Retrieval__EmbeddingGeneratorType"] = "Ollama";
        
        // Configure Redis as the vector store
        EndpointReference redisEndpoint = redis.GetEndpoint("redis");
        context.EnvironmentVariables["KernelMemory__DataIngestion__OrchestrationType"] = "Distributed";
        context.EnvironmentVariables["KernelMemory__DataIngestion__DistributedOrchestration__Queue__Type"] = "Redis";
        context.EnvironmentVariables["KernelMemory__DataIngestion__DistributedOrchestration__Queue__ConnectionString"] = CreateRedisConnectionString(redisEndpoint);
        context.EnvironmentVariables["KernelMemory__Retrieval__VectorDb__Type"] = "Redis";
        context.EnvironmentVariables["KernelMemory__Retrieval__VectorDb__ConnectionString"] = CreateRedisConnectionString(redisEndpoint);
    });

// Helper function to create standardized Redis connection string from endpoint reference
// Note: ContainerResource doesn't support WithReference directly, so we use WithEnvironment with endpoint references
static string CreateRedisConnectionString(EndpointReference endpoint) =>
    $"{endpoint.Host}:{endpoint.Port},abortConnect=false,connectRetry=5,connectTimeout=10000";

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("jaimes-api")
    .WithIconName("DocumentGlobe", IconVariant.Regular)
    .WithExternalHttpEndpoints()
    //.WithUrls(u => u.Urls.Clear())
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🌳 Root")
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/openapi/v1.json",
        DisplayText = "🌐 OpenAPI"
    })
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/swagger",
        DisplayText = "📃 Swagger"
    })
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/health",
        DisplayText = "👨‍⚕️ Health"
    })
    .WithReference(chatModel)
    .WithReference(embedModel)
    .WithReference(sqliteDb)
    .WithReference(qdrant)
    .WithReference(seq)
    .WaitFor(redis)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WaitFor(sqliteDb)
    .WaitFor(seq)
    .WaitFor(kernelMemory)
    .WithEnvironment(context =>
    {
        EndpointReference redisEndpoint = redis.GetEndpoint("redis");
        context.EnvironmentVariables["VectorDb__ConnectionString"] = CreateRedisConnectionString(redisEndpoint);

        // Explicitly set the SQLite connection string to ensure it's available as DefaultConnection
        context.EnvironmentVariables["ConnectionStrings__DefaultConnection"] =
            sqliteDb.Resource.ConnectionStringExpression;

        // Set Kernel Memory endpoint for MemoryWebClient
        EndpointReference kernelMemoryEndpoint = kernelMemory.GetEndpoint("http");
        context.EnvironmentVariables["KernelMemory__Endpoint"] = $"http://{kernelMemoryEndpoint.Host}:{kernelMemoryEndpoint.Port}";
    });

builder.AddProject<Projects.JAIMES_AF_Web>("jaimes-chat")
    .WithIconName("GameChat")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/health",
        DisplayText = "👨‍⚕️ Health"
    })
    .WithUrlForEndpoint("http", static url => url.DisplayText = "🏠 Home")
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/games",
        DisplayText = "🎮 Games"
    })
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/admin",
        DisplayText = "⚙️ Admin"
    })
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/scenarios",
        DisplayText = "📖 Scenarios"
    })
    .WithUrlForEndpoint("http", static _ => new()
    {
        Url = "/players",
        DisplayText = "👤 Players"
    })
    .WithReference(apiService)
    .WithReference(seq)
    .WaitFor(apiService)
    .WaitFor(seq);

builder.AddProject<Projects.JAIMES_AF_Indexer>("indexer")
    .WithIconName("DocumentSearch", IconVariant.Regular)
    .WithExplicitStart()
    .WithReference(embedModel)
    .WithReference(qdrant)
    .WithReference(seq)
    .WaitFor(ollama)
    .WaitFor(qdrant)
    .WaitFor(seq)
    .WaitFor(kernelMemory)
    .WithEnvironment(context =>
    {
        // Set Kernel Memory endpoint for MemoryWebClient
        EndpointReference kernelMemoryEndpoint = kernelMemory.GetEndpoint("http");
        context.EnvironmentVariables["KernelMemory__Endpoint"] = $"http://{kernelMemoryEndpoint.Host}:{kernelMemoryEndpoint.Port}";
        
        // Set Ollama endpoint URL for the indexer (if still needed for other purposes)
        // Use endpoint reference that resolves at runtime when endpoint is allocated
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        // Construct the URI expression - Aspire will resolve the {host} and {port} expressions at runtime
        context.EnvironmentVariables["Indexer__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        context.EnvironmentVariables["Indexer__OllamaModel"] = "nomic-embed-text";
    });

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentCrackerWorker>("document-cracker-worker")
    .WithIconName("DocumentTextExtract")
    .WithReference(rabbitmq)
    .WithReference(mongoDb)
    .WithReference(seq)
    .WaitFor(rabbitmq)
    .WaitFor(mongo)
    .WaitFor(seq);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentEmbeddings>("embedding-worker")
    .WithIconName("TextGrammarSettings")
    .WithReference(rabbitmq)
    .WithReference(mongoDb)
    .WithReference(qdrant)
    .WithReference(embedModel)
    .WithReference(seq)
    .WaitFor(rabbitmq)
    .WaitFor(mongo)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WaitFor(seq)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["EmbeddingWorker__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        
        // Set Qdrant endpoint and API key
        // QdrantServerResource uses "http" endpoint for the REST API (port 6333)
        // But QdrantClient uses gRPC which requires port 6334
Can         EndpointReference qdrantEndpoint = qdrant.GetEndpoint("http");
        context.EnvironmentVariables["EmbeddingWorker__QdrantHost"] = qdrantEndpoint.Host;
        // Use port 6334 for gRPC (QdrantClient uses gRPC, not HTTP REST API)
        context.EnvironmentVariables["EmbeddingWorker__QdrantPort"] = "6334";
        
        context.EnvironmentVariables["Qdrant__ApiKey"] = qdrantApiKey.Resource.ValueExpression;
    });

var app = builder.Build();

app.Run();
