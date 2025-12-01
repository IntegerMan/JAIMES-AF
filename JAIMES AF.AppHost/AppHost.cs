// Use polling instead of inotify to avoid watcher limits
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// We'll be consolidating our various datastores into PostgreSQL with JSONB and pgvector in the future,
var postgres = builder.AddPostgres("postgres")
        .WithIconName("DatabaseSwitch")
        .WithPgAdmin()
        .WithDataVolume(isReadOnly: false);
var postgresdb = postgres.AddDatabase("jaimes-db")
    .WithIconName("TaskListSquareDatabase");

// Add Ollama with nomic-embed-text model for embeddings
IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama-models")
    .WithIconName("BrainSparkle")
    .WithDataVolume();

// Note: versioning is important for embedding models to ensure consistency and reproducibility.
var embedModel = ollama.AddModel("embedModel", "nomic-embed-text:v1.5").WithIconName("CodeTextEdit");
var chatModel = ollama.AddModel("chatModel", "gemma3").WithIconName("CommentText");
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

// Add LavinMQ for messaging (wire-compatible with RabbitMQ)
var lavinmq = builder.AddLavinMQ("messaging")
    .WithIconName("DocumentQueue")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithUrls(u => {
        u.Urls.Clear();
        u.Urls.Add(new() { Url = "http://localhost:15672", DisplayText = "📋 Management" });
        u.Urls.Add(new() { Url = "http://localhost:15672/queues", DisplayText = "📬 Queues" });
        u.Urls.Add(new() { Url = "http://localhost:15672/consumers", DisplayText = "👥 Consumers" });
    });

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
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WaitFor(sqliteDb)
    .WaitFor(lavinmq)
    .WaitFor(mongo)
    .WithEnvironment(context =>
    {
        // Explicitly set the SQLite connection string to ensure it's available as DefaultConnection
        context.EnvironmentVariables["ConnectionStrings__DefaultConnection"] =
            sqliteDb.Resource.ConnectionStringExpression;
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
    .WaitFor(apiService);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentCrackerWorker>("document-cracker-worker")
    .WithIconName("DocumentTextExtract")
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WaitFor(lavinmq)
    .WaitFor(mongo);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChangeDetector>("document-change-detector")
    .WithIconName("DocumentSearch")
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WaitFor(lavinmq)
    .WaitFor(mongo);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChunking>("document-chunking-worker")
    .WithIconName("DocumentSplit")
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WithReference(qdrant)
    .WithReference(embedModel)
    .WaitFor(lavinmq)
    .WaitFor(mongo)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation (needed for SemanticChunker)
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentChunking__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        
        // Set Qdrant endpoint
        EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        context.EnvironmentVariables["DocumentChunking__QdrantHost"] = qdrantGrpcEndpoint.Host;
        context.EnvironmentVariables["DocumentChunking__QdrantPort"] = qdrantGrpcEndpoint.Port;
        context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] = qdrant.Resource.ConnectionStringExpression;
        
        // Set Qdrant API key (use the parameter value)
        context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;
    });

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentEmbedding>("document-embedding-worker")
    .WithIconName("DocumentEmbed")
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WithReference(qdrant)
    .WithReference(embedModel)
    .WaitFor(lavinmq)
    .WaitFor(mongo)
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentEmbedding__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        
        // Set Qdrant endpoint
        EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        context.EnvironmentVariables["DocumentEmbedding__QdrantHost"] = qdrantGrpcEndpoint.Host;
        context.EnvironmentVariables["DocumentEmbedding__QdrantPort"] = qdrantGrpcEndpoint.Port;
        context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] = qdrant.Resource.ConnectionStringExpression;
        
        // Set Qdrant API key (use the parameter value)
        context.EnvironmentVariables["qdrant-api-key"] = qdrantApiKey.Resource.ValueExpression;
    });

var app = builder.Build();

app.Run();
