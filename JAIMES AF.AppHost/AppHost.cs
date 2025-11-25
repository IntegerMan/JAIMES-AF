// Use polling instead of inotify to avoid watcher limits
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Add Ollama with nomic-embed-text model for embeddings
IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama-models")
    .WithIconName("BrainSparkle")
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

// Add parameter for DocumentChangeDetector content directory
var documentChangeDetectorContentDirectory = builder.AddParameter("document-change-detector-content-directory", "C:\\Dev\\Sourcebooks", secret: false)
    .WithDescription("Directory path to monitor for documents (e.g., C:\\Dev\\Sourcebooks)");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("jaimes-api")
    .WithIconName("DocumentGlobe", IconVariant.Regular)
    .WithExternalHttpEndpoints()
    //.WithUrls(u => u.Urls.Clear())
    .WithUrls(u => {
        u.Urls.Clear();
        u.Urls.Add(new() { Url = "/", DisplayText = "🌳 Root" });
        u.Urls.Add(new() { Url = "/openapi/v1.json", DisplayText = "🌐 OpenAPI" });
        u.Urls.Add(new() { Url = "/swagger", DisplayText = "📃 Swagger" });
        u.Urls.Add(new() { Url = "/health", DisplayText = "👨‍⚕️ Health" });
    })
    .WithHttpHealthCheck("/health")
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
    .WaitFor(mongo)
    .WithEnvironment("DocumentChangeDetector__ContentDirectory", documentChangeDetectorContentDirectory);

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentChunking>("document-chunking-worker")
    .WithIconName("DocumentSplit")
    .WithReference(lavinmq)
    .WithReference(mongoDb)
    .WithReference(embedModel)
    .WaitFor(lavinmq)
    .WaitFor(mongo)
    .WaitFor(ollama)
    .WithEnvironment(context =>
    {
        // Set Ollama endpoint for embedding generation (needed for SemanticChunker)
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["DocumentChunking__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
    });

builder.AddProject<Projects.JAIMES_AF_Workers_DocumentEmbeddings>("embedding-worker")
    .WithIconName("TextGrammarSettings")
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
        // The connection string from the model reference might not be automatically provided,
        // so we explicitly set the endpoint here
        EndpointReference ollamaEndpoint = ollama.GetEndpoint("http");
        context.EnvironmentVariables["EmbeddingWorker__OllamaEndpoint"] = $"http://{ollamaEndpoint.Host}:{ollamaEndpoint.Port}";
        
        // Set Qdrant endpoint
        // Use the gRPC endpoint (Primary endpoint) so host port mappings are respected
        EndpointReference qdrantGrpcEndpoint = qdrant.GetEndpoint("grpc");
        context.EnvironmentVariables["EmbeddingWorker__QdrantHost"] = qdrantGrpcEndpoint.Host;
        context.EnvironmentVariables["EmbeddingWorker__QdrantPort"] = qdrantGrpcEndpoint.Port;
        context.EnvironmentVariables["ConnectionStrings__qdrant-embeddings"] = qdrant.Resource.ConnectionStringExpression;
        
        // Note: API key should be extracted from the connection string by the worker
        // The connection string from WithReference(qdrant) should include the API key
        // We don't set it manually here because ValueExpression doesn't resolve in WithEnvironment
    });

var app = builder.Build();

app.Run();
