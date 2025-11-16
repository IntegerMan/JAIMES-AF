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
- **IChangeTracker / ChangeTracker**: Tracks document state using file hashes stored in JSON
- **IDocumentIndexer / DocumentIndexer**: Interfaces with Kernel Memory to index documents
- **IndexingOrchestrator**: Coordinates the indexing process

### Configuration

Configuration is provided via `appsettings.json`:
- `SourceDirectory`: Root directory to scan
- `VectorDbConnectionString`: SQLite connection string for Kernel Memory vector store
- `OpenAiEndpoint`, `OpenAiApiKey`, `OpenAiDeployment`: Azure OpenAI configuration
- `ChangeTrackingFile`: Path to JSON file storing document state (default: `indexer_tracking.json`)
- `SupportedExtensions`: List of file extensions to index (default: `.txt`, `.md`, `.pdf`, `.docx`)
- `Recursive`: Whether to process subdirectories recursively (default: `true`)

## Usage

1. **Configure the application** by editing `appsettings.json`:
   ```json
   {
     "Indexer": {
       "SourceDirectory": "C:\\Path\\To\\Your\\Documents",
       "VectorDbConnectionString": "Data Source=km_vector_store.db",
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
   - Process each document (add new, update changed, skip unchanged)
   - Log progress with appropriate log levels
   - Exit when complete

## Index Organization

Documents are organized by directory:
- Each subdirectory becomes its own Kernel Memory index
- Index names are generated from directory names (normalized and prefixed with `index-`)
- Files in the root directory are indexed under an index named `index-root`

## Change Tracking

The application tracks document state using SHA256 file hashes stored in `indexer_tracking.json`:
- **New documents**: Hash is computed and document is indexed
- **Changed documents**: Hash differs from stored value, document is re-indexed
- **Unchanged documents**: Hash matches stored value, document is skipped

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

This indexer populates the same vector store (`km_vector_store.db`) used by `RulesSearchService`:
- Documents indexed here are searchable via the RulesSearchService
- Index names should align with ruleset IDs for proper organization
- The vector store is shared, so both services can access the same indexed documents

