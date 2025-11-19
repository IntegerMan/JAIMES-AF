# JAIMES AF
**Join AI in Making Epic Stories - Agent Framework** Edition

A .NET 10 Aspire application for managing roleplaying games and scenarios using Microsoft Agent Framework and with a focus on observability and testing.

## Architecture

This solution uses several best practices and modern .NET libraries:

- **Scrutor** for automatic service registration by convention
- **Shouldly** for readable unit test assertions  
- **FluentValidation** for input validation
- **Serilog** for structured logging
- **Polly** for resilience patterns (retries, circuit breakers)
- **FastEndpoints** for minimal API endpoints
- **Entity Framework Core** with multi-database support

## Database Configuration

The application uses **SQLite by default** for cross-platform compatibility. The database file (`jaimes.db`) will be created automatically on first run with all necessary tables and seed data.

### Using SQL Server / LocalDB (Windows only)

If you prefer to use SQL Server/LocalDB on Windows, configure it via user secrets:

```bash
cd "JAIMES AF.ApiService"
dotnet user-secrets set "DatabaseProvider" "SqlServer"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=Jaimes;Trusted_Connection=True;MultipleActiveResultSets=true"
```

**Important:** The included migrations are SQLite-specific. To use SQL Server:
1. Delete your existing `jaimes.db` file (if it exists)
2. Remove all migrations: `rm -rf "JAIMES AF.Repositories/Migrations"`
3. Regenerate migrations for SQL Server:
   ```bash
   dotnet ef migrations add InitialCreate --project "JAIMES AF.Repositories" --startup-project "JAIMES AF.ApiService"
   ```
4. The database will be created automatically on first run

### Azure SQL

For production cloud deployments, configure via user secrets or environment variables:

```bash
dotnet user-secrets set "DatabaseProvider" "SqlServer"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:{your-server}.database.windows.net,1433;Database=Jaimes;User ID={user};Password={password};Encrypt=True;"
```

Note: You'll need to regenerate migrations for SQL Server (see instructions above) before deploying to Azure SQL.

## Redis Dependency

The application uses Redis as the vector store backend for Kernel Memory. You must have Redis running locally before starting the application.

### Running Redis with Docker

```bash
docker run -d --name redis-stack -p 6379:6379 -p 8001:8001 -v redis-data:/data redis/redis-stack:latest
```

This command:
- Starts Redis Stack in detached mode
- Maps Redis port 6379 to your host
- Maps RedisInsight web UI to port 8001 (optional, for monitoring)
- Creates a named volume `redis-data` for data persistence

### Running Redis with Podman

```bash
podman run -d --name redis-stack -p 6379:6379 -p 8001:8001 -v redis-data:/data redis/redis-stack:latest
```

### Verifying Redis is Running

Test the connection:
```bash
# Docker
docker exec -it redis-stack redis-cli ping

# Podman
podman exec -it redis-stack redis-cli ping
```

You should receive a `PONG` response.

### Accessing RedisInsight (Web UI)

Open your browser and navigate to:
```
http://localhost:8001
```

### Redis Configuration

The application connects to Redis at `localhost:6379` by default. This can be configured in `appsettings.json`:

```json
{
  "VectorDb": {
    "ConnectionString": "localhost:6379"
  }
}
```

For production or custom configurations, you can use full connection strings:
- `localhost:6379` - Default local connection
- `localhost:6379,password=yourpassword` - With password
- `redis://localhost:6379` - Full Redis URL format

## Chat Service Configuration

The application requires Azure OpenAI with the following deployment:

- **Model Deployment**: `gpt-4o-mini` (or update the `Deployment` setting in appsettings.json)

Configure your Azure OpenAI credentials via user secrets:

```bash
cd "JAIMES AF.ApiService"
dotnet user-secrets set "ChatService:Endpoint" "https://YourResource.openai.azure.com/"
dotnet user-secrets set "ChatService:ApiKey" "your-actual-api-key-here"
```

Replace `YourResource` with your Azure OpenAI resource name and provide your actual API key.

## Project Structure

- **JAIMES AF.ApiService** - FastEndpoints-based API
- **JAIMES AF.Web** - Blazor web frontend
- **JAIMES AF.Services** - Business logic layer
- **JAIMES AF.Repositories** - Data access layer with EF Core
- **JAIMES AF.ServiceDefaults** - Shared Aspire service defaults
- **JAIMES AF.AppHost** - Aspire orchestration
- **JAIMES AF.Tests** - Unit and integration tests organized by layer:
  - `Endpoints/` - API endpoint tests
  - `Services/` - Business logic tests
  - `Repositories/` - Data access tests

## Running the Application

**Prerequisites:**
1. Ensure Redis is running locally (see [Redis Dependency](#redis-dependency) section above)
2. Configure Azure OpenAI credentials (see [Chat Service Configuration](#chat-service-configuration) section above)

```bash
dotnet run --project "JAIMES AF.AppHost"
```

## Running Tests

```bash
# Run all tests (excluding Aspire integration tests which require DCP)
dotnet test --filter "FullyQualifiedName!~WebTests"

# Run only endpoint tests
dotnet test --filter "FullyQualifiedName~Endpoints"

# Run only service tests
dotnet test --filter "FullyQualifiedName~Services"

# Run only repository tests
dotnet test --filter "FullyQualifiedName~Repositories"
```

## Development

Tests use Shouldly for more readable assertions:

```csharp
// Instead of
Assert.Equal(expected, actual);
Assert.NotNull(result);

// Use
actual.ShouldBe(expected);
result.ShouldNotBeNull();
```

Services are registered automatically using Scrutor - just implement an interface and it will be discovered and registered.
