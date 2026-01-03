# Getting Started with JAIMES AF

This guide covers everything you need to get up and running with JAIMES AF, including prerequisites, configuration, and development guidelines.

## Prerequisites

- **.NET 10 SDK** (or latest .NET Core version as specified in `global.json`)
- **Docker Desktop** (or equivalent container runtime). Required by Aspire to provision PostgreSQL, Qdrant, LavinMQ, and other dependencies.
- **Azure OpenAI Resource** (or a local Ollama instance) for chat and embeddings.

## Rapid Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/your-org/JAIMES-AF.git
   cd JAIMES-AF
   ```

2. **Configure Secrets**:
   Set up your Azure OpenAI credentials (or skip to Ollama configuration).
   ```bash
   cd "JAIMES AF.ApiService"
   dotnet user-secrets set "ChatService:Endpoint" "https://YourResource.openai.azure.com/"
   dotnet user-secrets set "ChatService:ApiKey" "your-actual-api-key"
   dotnet user-secrets set "ChatService:Name" "gpt-4o-mini"
   ```

3. **Run the Application**:
   Start the Aspire AppHost.
   ```bash
   dotnet run --project "JAIMES AF.AppHost"
   ```

4. **Verify**:
   Open the **Aspire Dashboard** (URI printed in console) to view running services, logs, and database status.

## Configuration Details

### Database

- **PostgreSQL (Default)**: Provisioned automatically by Aspire via the `postgres-db` connection. Migrations apply on startup.
- **Connection Strings**:
  - Local Dev (Aspire): Managed automatically.
  - Local Dev (Standalone): See `appsettings.Development.json` (defaults to `localhost:5432`).
  - Production: Use environment variables or Key Vault.

### Vector Storage (Qdrant)

- **Qdrant**: Used for vector search (Rulesets and Conversations). Provisioned by Aspire. Persistent data is stored in Docker volumes. Accessible via the Aspire dashboard.

### AI Providers (Chat & Embeddings)

Configure via `appsettings.json` or User Secrets.

**Azure OpenAI (Recommended)**
```bash
dotnet user-secrets set "ChatService:Provider" "AzureOpenAi"
dotnet user-secrets set "ChatService:Endpoint" "..."
dotnet user-secrets set "ChatService:ApiKey" "..."
```

**Ollama (Local)**
```bash
dotnet user-secrets set "ChatService:Provider" "Ollama"
dotnet user-secrets set "ChatService:Endpoint" "http://localhost:11434"
dotnet user-secrets set "ChatService:Name" "gemma3"
```

## Running Tests

Run the full suite using the Microsoft Test Platform:

```bash
dotnet test
```

Or target specific projects:

```bash
dotnet run --project "JAIMES AF.Tests/JAIMES AF.Tests.csproj" -- --filter "FullyQualifiedName~Services"
```

## Development Guidelines

### Layered Architecture Principles

- **ApiService**: Only for HTTP surface, request/response models, and simple validation. No deep business logic.
- **Services**: The core business logic layer. Contains orchestrators, mappers, and AI integration contracts.
- **Repositories**: EF Core entities, DbContext, and migrations. Pure data access.
- **Workers**: Background processing pipelines (document cracking, embeddings).

### Service Design

- Prefer small, single-responsibility services.
- Define interfaces (`IGameService`) for all services to enable easy DI and testing.
- Use explicit Mappers (e.g., `GameMapper`) to translate between Entities <-> DTOs.

### Coding Style & Conventions

- **Clean Code**: Use clear, expressive names. Keep methods under ~60 lines.
- **Immutability**: Prefer immutable DTOs and records.
- **Type Safety**: Prefer target-typed `new` (`Message m = new();`) except for `foreach` loops or obvious types.
- **Async/Await**: Use `async` all the way down. Accept `CancellationToken` in async methods.

### Database Migrations

Use these handy commands to manage EF Core migrations:

Check for pending model changes:
```bash
dotnet ef migrations has-pending-model-changes --project '.\JAIMES AF.Repositories\JAIMES AF.Repositories.csproj' --startup-project '.\JAIMES AF.ApiService\JAIMES AF.ApiService.csproj'
```

Add a new migration:
```bash
dotnet ef migrations add <MigrationName> --project '.\JAIMES AF.Repositories\JAIMES AF.Repositories.csproj' --startup-project '.\JAIMES AF.ApiService\JAIMES AF.ApiService.csproj'
```

Update local database:
```bash
dotnet ef database update --project '.\JAIMES AF.Repositories\JAIMES AF.Repositories.csproj' --startup-project '.\JAIMES AF.ApiService\JAIMES AF.ApiService.csproj'
```
