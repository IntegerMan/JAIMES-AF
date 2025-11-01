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

The application supports multiple database providers through environment-based configuration:

### SQLite (Default)
Best for local development and testing. No setup required.

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=jaimes-dev.db"
  }
}
```

### SQL Server / LocalDB
For production or when you need SQL Server features.

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=Jaimes;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Azure SQL
For cloud deployments.

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:{your-server}.database.windows.net,1433;Database=Jaimes;..."
  }
}
```

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
