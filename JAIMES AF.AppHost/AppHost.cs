// Use polling instead of inotify to avoid watcher limits

Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

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
        u.Urls.Add(new ResourceUrlAnnotation {Url = "http://localhost:5858", DisplayText = "📋 pgAdmin"});
    });
});

IResourceBuilder<PostgresDatabaseResource> postgresdb = postgres.AddDatabase("postgres-db", "postgres")
    .WithCreationScript("CREATE EXTENSION IF NOT EXISTS vector;");


// Add Ollama with nomic-embed-text model for embeddings
IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama-models")
    .WithIconName("BrainSparkle")
    .WithDataVolume();

// Note: versioning is important for embedding models to ensure consistency and reproducibility.
IResourceBuilder<OllamaModelResource> embedModel =
    ollama.AddModel("embedModel", "nomic-embed-text:v1.5").WithIconName("CodeTextEdit");
IResourceBuilder<OllamaModelResource> chatModel = ollama.AddModel("chatModel", "gemma3").WithIconName("CommentText");
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
        u.Urls.Add(new ResourceUrlAnnotation {Url = "http://localhost:15672", DisplayText = "📋 Management"});
        u.Urls.Add(new ResourceUrlAnnotation {Url = "http://localhost:15672/queues", DisplayText = "📬 Queues"});
        u.Urls.Add(new ResourceUrlAnnotation {Url = "http://localhost:15672/consumers", DisplayText = "👥 Consumers"});
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
        })
    .WithReference(chatModel)
    .WithReference(embedModel)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithReference(lavinmq)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(lavinmq);

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

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChunking>("document-chunking-worker")
    .WithIconName("DocumentSplit")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithReference(embedModel)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation (needed for SemanticChunker)
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentChunking__OllamaEndpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";

        // Set Qdrant endpoint
        EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        context.EnvironmentVariables["DocumentChunking__QdrantHost"] = qdrantGrpcEndpoint.Host;
        context.EnvironmentVariables["DocumentChunking__QdrantPort"] = qdrantGrpcEndpoint.Port;
        context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] =
            qdrant.Resource.ConnectionStringExpression;

        // Set Qdrant API key (use the parameter value)
        context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;
    });

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentEmbedding>("document-embedding-worker")
    .WithIconName("DocumentEmbed")
    .WithReference(lavinmq)
    .WithReference(postgresdb)
    .WithReference(qdrant)
    .WithReference(embedModel)
    .WaitFor(lavinmq)
    .WaitFor(postgres)
    .WaitFor(postgresdb)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentEmbedding__OllamaEndpoint"] =
            $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";

        // Set Qdrant endpoint
        EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        context.EnvironmentVariables["DocumentEmbedding__QdrantHost"] = qdrantGrpcEndpoint.Host;
        context.EnvironmentVariables["DocumentEmbedding__QdrantPort"] = qdrantGrpcEndpoint.Port;
        context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] =
            qdrant.Resource.ConnectionStringExpression;

        // Set Qdrant API key (use the parameter value)
        context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;
    });

DistributedApplication app = builder.Build();

app.Run();