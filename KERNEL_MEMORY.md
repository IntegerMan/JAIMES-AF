# Kernel Memory and Rules Search

## Overview

Kernel Memory is used in this project to provide RAG (Retrieval-Augmented Generation) search capabilities over rulesets. This allows AI agents to query game rules using natural language and receive relevant rule information.

## Architecture

### Storage Model
- **Rules are NOT stored in EF entities** - they exist only in Kernel Memory's vector store
- Vector store: Uses a directory structure (configured via `WithSimpleVectorDb()` with a directory path)
- The directory path can be specified directly or extracted from a connection string format (e.g., `"Data Source=km_vector_store"` becomes `"km_vector_store"`)
- Rules are indexed by `rulesetId` which serves as an index/filter for organizing and searching

### Key Components

1. **IRulesSearchService** (`JAIMES AF.ServiceDefinitions/Services/IRulesSearchService.cs`)
   - Interface defining rules search operations
   - Methods: `SearchRulesAsync()`, `IndexRuleAsync()`, `EnsureRulesetIndexedAsync()`

2. **RulesSearchService** (`JAIMES AF.Agents/Services/RulesSearchService.cs`)
   - Implementation using Kernel Memory
   - Configures Kernel Memory with Azure OpenAI and directory-based vector store
   - Handles indexing and searching of rules

3. **RulesSearchTool** (`JAIMES AF.Tools/RulesSearchTool.cs`)
   - Tool registered with AI agents
   - Allows agents to search rules using natural language queries
   - Automatically uses the current game's ruleset ID

## Configuration

### Kernel Memory Setup

```csharp
// Configure Azure OpenAI
OpenAIConfig openAiConfig = new()
{
    APIKey = chatOptions.ApiKey,
    Endpoint = chatOptions.Endpoint,
    TextModel = chatOptions.Deployment,
    EmbeddingModel = chatOptions.Deployment
};

// Configure directory-based vector store
// WithSimpleVectorDb expects a directory path (not a connection string)
// The path can be extracted from a connection string format for backward compatibility
string vectorDbPath = "km_vector_store"; // or extract from "Data Source=km_vector_store"

// Build Kernel Memory instance
IKernelMemory memory = new KernelMemoryBuilder()
    .WithOpenAI(openAiConfig)
    .WithSimpleVectorDb(vectorDbPath)
    .Build();
```

### Package Dependencies
- `Microsoft.KernelMemory.Core` - Core Kernel Memory functionality
- Uses `WithSimpleVectorDb()` for directory-based vector storage (built into core package)

## Usage

### Indexing Rules

Rules are indexed into Kernel Memory using `ImportTextAsync()`:

```csharp
await memory.ImportTextAsync(
    text: fullContent,                    // Rule content (title + content)
    documentId: $"rule-{ruleId}",         // Unique document ID
    index: GetIndexName(rulesetId),       // Index name based on rulesetId
    tags: new TagCollection
    {
        { "rulesetId", rulesetId },      // Primary filter tag
        { "ruleId", ruleId },
        { "title", title }
    },
    cancellationToken: cancellationToken);
```

### Searching Rules

Rules are searched using `AskAsync()` with filtering by `rulesetId`:

```csharp
MemoryAnswer answer = await memory.AskAsync(
    question: query,                      // Natural language query
    index: GetIndexName(rulesetId),       // Search within ruleset index
    filters: new List<MemoryFilter> 
    { 
        new MemoryFilter().ByTag("rulesetId", rulesetId) 
    },
    cancellationToken: cancellationToken);

string result = answer.Result;           // LLM-generated answer based on relevant rules
```

### Index Naming

Index names are generated from ruleset IDs:
```csharp
private static string GetIndexName(string rulesetId)
{
    return $"ruleset-{rulesetId.ToLowerInvariant()}";
}
```

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

4. **Separate Storage**: The vector store (directory structure) is separate from the main application database, allowing for independent management and optimization.

## References

- **Kernel Memory Blog Post**: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
- **Kernel Memory GitHub**: https://github.com/microsoft/kernel-memory
- **Package**: `Microsoft.KernelMemory.Core` (NuGet)

## Future Considerations

- Consider adding a management API for indexing/updating rules
- May want to add caching for frequently searched rules
- Consider adding metrics/monitoring for search performance
- Could add support for rule versioning if rules change over time

