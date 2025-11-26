# Kernel Memory and Rules Search

## Overview

Kernel Memory is used in this project to provide RAG (Retrieval-Augmented Generation) search capabilities over rulesets. This allows AI agents to query game rules using natural language and receive relevant rule information.

## Architecture

### Storage Model
- **Rules are NOT stored in EF entities** - they exist only in Kernel Memory's vector store
- Vector store: Uses Redis as the backend (configured via `WithRedisMemoryDb()` with a Redis connection string)
- Redis connection string format: `"localhost:6379"` or `"localhost:6379,password=xxx"` or full Redis URL
- Rules are indexed by `rulesetId` which serves as an index/filter for organizing and searching
- **Redis Dependency**: Redis must be running locally before starting the application

### Key Components

1. **IRulesSearchService** (`JAIMES AF.ServiceDefinitions/Services/IRulesSearchService.cs`)
   - Interface defining rules search operations
   - Methods: `SearchRulesAsync()`, `IndexRuleAsync()`, `EnsureRulesetIndexedAsync()`

2. **RulesSearchService** (`JAIMES AF.Agents/Services/RulesSearchService.cs`)
   - Implementation using Kernel Memory
   - Configures Kernel Memory with Azure OpenAI and Redis vector store
   - Handles indexing and searching of rules

3. **RulesSearchTool** (`JAIMES AF.Tools/RulesSearchTool.cs`)
   - Tool registered with AI agents
   - Allows agents to search rules using natural language queries
   - Automatically uses the current game's ruleset ID

## Configuration

### Redis Setup

Before configuring Kernel Memory, you must have Redis running locally.

#### Running Redis with Docker

```bash
docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 -v redis-data:/data redis/redis-stack:latest
```

This command:
- Starts Redis Stack in detached mode
- Maps Redis port 6379 to your host
- Maps RedisInsight web UI to port 8001 (optional, for monitoring)
- Creates a named volume `redis-data` for data persistence

#### Verifying Redis is Running

Test the connection:
```bash
docker exec -it redis-stack redis-cli ping
```

You should receive a `PONG` response.

#### Accessing RedisInsight (Web UI)

Open your browser and navigate to:
```
http://localhost:8001
```

### Kernel Memory Setup

```csharp
// Configure Azure OpenAI
AzureOpenAIConfig embeddingConfig = new()
{
    APIKey = chatOptions.ApiKey,
    Auth = AzureOpenAIConfig.AuthTypes.APIKey,
    Endpoint = normalizedEndpoint,
    Deployment = chatOptions.EmbeddingDeployment,
};

AzureOpenAIConfig textGenerationConfig = new()
{
    APIKey = chatOptions.ApiKey,
    Auth = AzureOpenAIConfig.AuthTypes.APIKey,
    Endpoint = normalizedEndpoint,
    Deployment = chatOptions.TextGenerationDeployment,
};

// Configure Redis as the vector store
// Connection string format: "localhost:6379" or "localhost:6379,password=xxx"
string redisConnectionString = "localhost:6379";

// Build Kernel Memory instance
IKernelMemory memory = new KernelMemoryBuilder()
    .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
    .WithAzureOpenAITextGeneration(textGenerationConfig)
    .WithRedisMemoryDb(redisConnectionString)
    .Build();
```

### Package Dependencies
- `Microsoft.KernelMemory.Core` - Core Kernel Memory functionality
- `Microsoft.KernelMemory.MemoryDb.Redis` - Redis memory database connector
- Uses `WithRedisMemoryDb()` for Redis-based vector storage

## Usage

### Indexing Rules

Rules are indexed into Kernel Memory using `ImportTextAsync()`. All rulesets are stored in a single unified "rulesets" index, with filtering by specific ruleset done via tags:

```csharp
await memory.ImportTextAsync(
    text: fullContent,                    // Rule content (title + content)
    documentId: $"rule-{ruleId}",         // Unique document ID
    index: "rulesets",                     // All rulesets use the same index
    tags: new TagCollection
    {
        { "rulesetId", rulesetId },      // Primary filter tag for ruleset filtering
        { "ruleId", ruleId },
        { "title", title }
    },
    cancellationToken: cancellationToken);
```

### Searching Rules

Rules are searched using `SearchAsync()` with optional filtering by `rulesetId` tag:

```csharp
// Search within a specific ruleset
List<MemoryFilter> filters = [new MemoryFilter().ByTag("rulesetId", rulesetId)];
SearchResult result = await memory.SearchAsync(
    query: query,                          // Natural language query
    index: "rulesets",                     // Search the unified rulesets index
    filters: filters,                      // Filter by rulesetId tag
    limit: 10,
    cancellationToken: cancellationToken);

// Search across all rulesets (no filter)
SearchResult result = await memory.SearchAsync(
    query: query,
    index: "rulesets",                     // Search the unified rulesets index
    filters: null,                          // No filter = search all rulesets
    limit: 10,
    cancellationToken: cancellationToken);
```

### Index Naming

All rulesets are stored in a single unified index:
```csharp
private static string GetIndexName()
{
    return "rulesets";  // All rulesets use the same index
}
```

Filtering by specific ruleset is done via the `rulesetId` tag when searching.

## Integration with AI Agents

### Tool Registration

The `RulesSearchTool` is automatically registered in `ChatService.CreateAgent()`:

```csharp
if (this.rulesSearchService != null)
{
    RulesSearchTool rulesSearchTool = new(game, this.rulesSearchService);
    
    AIFunction rulesSearchFunction = AIFunctionFactory.Create(
        (string query) => rulesSearchTool.SearchRulesAsync(query),
        name: "SearchRules",
        description: "Searches the ruleset's indexed rules to find answers to specific questions or queries...");
    toolList.Add(rulesSearchFunction);
}
```

### Tool Description

The tool is described to agents as:
> "Searches the ruleset's indexed rules to find answers to specific questions or queries. This is a rules search tool that gets answers from rules to specific questions or queries. Use this tool whenever you need to look up game rules, mechanics, or rule clarifications."

## Important Design Decisions

1. **No EF Entities for Rules**: Rules are stored only in the vector database, not in the main application database. This keeps the vector store separate and optimized for semantic search.

2. **RulesetId as Index**: The `rulesetId` is used as an index/filter, not as a database foreign key. It's purely for organizing and filtering rules in the vector store.

3. **Automatic Indexing**: Rules should be indexed via `IndexRuleAsync()` when they are added to the system. The `EnsureRulesetIndexedAsync()` method is kept for interface compatibility but doesn't query EF entities.

4. **Separate Storage**: The vector store (Redis) is separate from the main application database, allowing for independent management and optimization.

## References

- **Kernel Memory Blog Post**: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
- **Kernel Memory GitHub**: https://github.com/microsoft/kernel-memory
- **Packages**: 
  - `Microsoft.KernelMemory.Core` (NuGet) - Core Kernel Memory functionality
  - `Microsoft.KernelMemory.MemoryDb.Redis` (NuGet) - Redis memory database connector

## Future Considerations

- Consider adding a management API for indexing/updating rules
- May want to add caching for frequently searched rules
- Consider adding metrics/monitoring for search performance
- Could add support for rule versioning if rules change over time

