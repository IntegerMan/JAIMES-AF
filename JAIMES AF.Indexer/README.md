# JAIMES AF.Indexer

A non-interactive .NET 10 console application that indexes documents from a configured directory structure into Kernel Memory's vector store for use with the RulesSearchService.

## Overview

This application:
- Scans a configured source directory and its subdirectories
- Finds all indexable documents (based on configured file extensions)
- Tracks document changes using file hashes
- Indexes new documents, updates changed documents, and skips unchanged documents
- Organizes documents by directory into separate Kernel Memory indexes
- Uses appropriate log levels for different operations

## Architecture

The application is designed with modularity in mind, making it easy to convert to a worker service in the future:

### Services

- **IDirectoryScanner / DirectoryScanner**: Scans directories and enumerates files
- **IChangeTracker / ChangeTracker**: Computes SHA256 file hashes for change detection
- **IDocumentIndexer / DocumentIndexer**: Interfaces with Kernel Memory to index documents and check document status
- **IndexingOrchestrator**: Coordinates the indexing process

### Configuration

Configuration is provided via `appsettings.json`:
- `SourceDirectory`: Root directory to scan
- `VectorDbConnectionString`: Directory path for Kernel Memory vector store (e.g., `"C:\\Dev\\JAIMES AF\\VectorStore"` - can also use `"Data Source=path"` format for backward compatibility, must match RulesSearchService)
- `OpenAiEndpoint`, `OpenAiApiKey`, `OpenAiDeployment`: Azure OpenAI configuration (only EmbeddingModel is used, not TextModel)
- `SupportedExtensions`: List of file extensions to index (default: `.txt`, `.md`, `.pdf`, `.docx`)
- `Recursive`: Whether to process subdirectories recursively (default: `true`)

## Azure OpenAI Setup

Before configuring the application, you need to create an Azure OpenAI resource and deployment. This indexer only requires an embedding model (not a text generation model).

### Creating the Azure OpenAI Resource

1. **Create an Azure OpenAI resource** in the Azure Portal:
   - Navigate to Azure Portal → Create a resource → Azure OpenAI
   - Select your subscription, resource group, and region
   - Choose a pricing tier appropriate for your needs

2. **Create an embedding model deployment**:
   - In your Azure OpenAI resource, go to "Deployments" → "Create"
   - **Deployment name**: `text-embedding-3-small-global` (or your preferred name)
   - **Deployment type**: Select "Global Standard" (pay per API call with highest rate limits)
   - **Model**: Select `text-embedding-3-small` (or another embedding model)
   - **Model version**: `1 (Default)`
   - **Model version upgrade policy**: "Opt out of automatic model version upgrades" (recommended for embedding models)
   - **Tokens per Minute Rate Limit**: Set based on your needs (e.g., 501K tokens/min ≈ 3K requests/min)
   - **Content filter**: Select your content filter policy

3. **Get your credentials**:
   - **Endpoint**: Found in your Azure OpenAI resource under "Keys and Endpoint" (format: `https://your-resource-name.openai.azure.com/`)
   - **API Key**: Found in the same section (you can use either Key 1 or Key 2)
   - **Deployment name**: The name you used when creating the deployment (e.g., `text-embedding-3-small-global`)

### Recommended Deployment Settings

Based on this project's configuration:
- **Deployment name**: `text-embedding-3-small-global`
- **Model**: `text-embedding-3-small`
- **Deployment type**: Global Standard
- **Rate limit**: 501K tokens per minute (provides ~3K requests per minute)

**Note**: The indexer only uses the embedding model for vectorizing documents. You do not need a text generation model (like GPT-4) for this application.

## Usage

1. **Configure the application**:
   
   **Option A: Using User Secrets (Recommended for sensitive data)**
   
   Set the API key and endpoint using user secrets:
   ```bash
   dotnet user-secrets set "Indexer:OpenAiApiKey" "your-api-key" --project "JAIMES AF.Indexer"
   dotnet user-secrets set "Indexer:OpenAiEndpoint" "https://your-endpoint.openai.azure.com/" --project "JAIMES AF.Indexer"
   ```
   
   **Option B: Using appsettings.json**
   
   Edit `appsettings.json`:
   ```json
   {
     "Indexer": {
       "SourceDirectory": "C:\\Your\\Directory\\Sourcebooks",
       "VectorDbConnectionString": "C:\\Dev\\JAIMES AF\\VectorStore",
       "OpenAiEndpoint": "https://your-endpoint.openai.azure.com/",
       "OpenAiApiKey": "your-api-key",
       "OpenAiDeployment": "your-deployment-name"
     }
   }
   ```

2. **Run the application**:
   ```bash
   dotnet run --project "JAIMES AF.Indexer"
   ```

3. The application will:
   - Scan the source directory and all subdirectories
   - Process each document (add new or update existing)
   - Log progress with appropriate log levels
   - Exit when complete

## Index Organization

Documents are organized by directory:
- Each subdirectory becomes its own Kernel Memory index
- Index names are generated from directory names (normalized and prefixed with `index-`)
- Files in the root directory are indexed under an index named `index-root`

## Change Tracking

The application uses Kernel Memory's document overwrite behavior for change tracking:
- **New documents**: Document is indexed with file hash stored as a tag
- **Existing documents**: Document status is checked, then re-indexed (Kernel Memory overwrites documents with the same `documentId`)
- **No duplicates**: Kernel Memory ensures that re-indexing with the same `documentId` overwrites the existing document rather than creating duplicates
- **Hash tagging**: File hashes are computed and stored as tags for potential future use, but are not used for comparison

## Logging

The application uses structured logging with appropriate log levels:
- **Information**: Start/end of process, directory processing, document additions/updates
- **Debug**: File scanning details, skipped documents
- **Warning**: Missing directories, file access issues
- **Error**: Indexing failures, unexpected exceptions

## Future Worker Service Integration

The modular design allows easy conversion to a worker service:
- Services are already registered via dependency injection
- `IndexingOrchestrator` can be called from a `BackgroundService`
- Configuration can be shared with the main application
- The same Kernel Memory instance can be shared across services

## Integration with RulesSearchService

This indexer uses the same vector store as `RulesSearchService`:
- **Shared Storage**: Both services use the same vector store directory path (specified in `VectorDbConnectionString` configuration)
- **Same Configuration**: The `VectorDbConnectionString` in `appsettings.json` should match the path used by `RulesSearchService` (can be a direct directory path like `"C:\\Dev\\JAIMES AF\\VectorStore"` or use `"Data Source=path"` format for backward compatibility)
- **Document Access**: Documents indexed here are searchable via the RulesSearchService
- **Index Organization**: Index names are generated from directory names (prefixed with `index-`), while RulesSearchService uses `ruleset-` prefix for ruleset indexes
- **Storage Format**: The vector store uses a directory structure (not SQLite), managed by Kernel Memory's `WithSimpleVectorDb()` method

