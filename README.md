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

The application uses Redis as the vector store backend for Kernel Memory. **Redis is automatically managed by Aspire** when you run the AppHost project - no manual setup is required.

### Prerequisites

**Important**: Aspire requires a container runtime to manage Redis. The application uses Docker Desktop.

- **Docker Desktop**: Ensure Docker Desktop is installed and running on Windows
  - Download from: https://www.docker.com/products/docker-desktop/
  - After installation, ensure Docker Desktop is running before starting the AppHost
  - You can verify Docker is running by executing `docker ps` in a terminal

Aspire will automatically detect and use Docker when it's available and healthy.

### Troubleshooting Docker Issues

If you see an error message like "Container runtime 'docker' was found but appears to be unhealthy":

1. **Verify Docker Desktop is running**:
   - Look for the Docker Desktop icon in your system tray (whale icon)
   - If it's not running, start Docker Desktop from the Start menu
   - Wait for Docker Desktop to fully start (the icon should be steady, not animating)

2. **Test Docker connectivity**:
   ```bash
   docker ps
   ```
   - If this command succeeds (shows a list of containers or an empty list), Docker is working
   - If it fails with a connection error, Docker Desktop is not running or not accessible

3. **Check Docker Desktop settings**:
   - Open Docker Desktop
   - Go to Settings → General
   - Ensure "Use the WSL 2 based engine" is enabled (if using WSL)
   - Ensure "Start Docker Desktop when you log in" is enabled (optional, for convenience)

4. **Restart Docker Desktop**:
   - Right-click the Docker Desktop icon in the system tray
   - Select "Quit Docker Desktop"
   - Wait a few seconds, then start Docker Desktop again
   - Wait for it to fully start before running the AppHost

5. **Check for port conflicts**:
   - Ensure no other containers are using the ports required by Aspire (e.g., 6379 for Redis)
   - Stop any manually started Redis containers: `docker stop redis-stack` (if applicable)

### Redis Management

- **Automatic Startup**: Redis Stack is automatically started when you run the Aspire AppHost
- **Data Persistence**: Redis data is persisted between sessions in a local directory (`%LocalAppData%\Aspire\jaimes-redis-data` on Windows)
- **RedisInsight**: RedisInsight web UI is available at `http://localhost:8001` for monitoring and managing Redis data
- **Dashboard Integration**: Redis appears in the Aspire dashboard for monitoring and management

### Redis Configuration

The application automatically connects to the Redis instance managed by Aspire. The connection string is configured automatically and does not need to be set manually.

For advanced scenarios, you can override the connection string in `appsettings.json`:

```json
{
  "VectorDb": {
    "ConnectionString": "localhost:6379"
  }
}
```

**Note**: If you have an existing Redis instance running (e.g., from Docker), you should stop it before running the AppHost to avoid port conflicts.

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
