Agents Orientation

Purpose

This file provides minimal, machine-oriented hints and common symbols to help AI assistants (Copilot, GitHub Copilot, and other agents) understand the repository structure and naming conventions.

Key service interfaces and locations

- `IGameService` -> `JAIMES AF.Services/Services/IGameService.cs`
- `IScenariosService` -> `JAIMES AF.Services/Services/IScenariosService.cs`
- `IPlayersService` -> `JAIMES AF.Services/Services/IPlayersService.cs`
- `ChatService` -> `JAIMES AF.Agents/Services/ChatService.cs`
- `IRulesSearchService` -> `JAIMES AF.ServiceDefinitions/Services/IRulesSearchService.cs`
- `RulesSearchService` -> `JAIMES AF.Agents/Services/RulesSearchService.cs`

Common DTOs and Mappers

- DTOs: `Services/Models/*.cs` (e.g., `GameDto`, `ScenarioDto`, `PlayerDto`)
- Mappers: `Services/Services/*Mapper.cs` (e.g., `GameMapper`, `MessageMapper`)

API endpoints

- `JAIMES AF.ApiService/Endpoints/*.cs` - small endpoint classes
- Request/Response models in `JAIMES AF.ApiService/Requests` and `Responses`

EF Core entities

- `JAIMES AF.Repositories/Entities/*.cs` - EF Core entities
- `JaimesDbContext` - in `JAIMES AF.Repositories/JaimesDbContext.cs`

Testing

- Tests are in `JAIMES AF.Tests/` with folders for endpoints, repositories, and services.
- **CRITICAL**: When making code changes, you MUST:
  1. Update existing tests to reflect any changes to data structures, behavior, or APIs
  2. Add new tests when introducing new functionality or fixing bugs
  3. Ensure all tests pass before considering a task complete
  4. Use Shouldly for all assertions (never use `Assert.*` methods)
  5. Verify that tests cover edge cases, especially when fixing bugs (e.g., if fixing ordering, test with identical timestamps)
- When adding properties to DTOs or responses, update tests to verify those properties are populated correctly
- When changing ordering logic, add tests that verify the ordering works correctly, including edge cases

Development notes

- Target: `.NET10`
- DI registration: `ServiceCollectionExtensions.cs` in each layer registers services and repositories.
- Keep endpoints thin and delegate to services.

Kernel Memory and Rules Search

- **Kernel Memory**: Used for RAG (Retrieval-Augmented Generation) search over rulesets
- **RulesSearchService**: Provides rules search functionality using Kernel Memory
- **Storage**: Rules are stored in Kernel Memory's directory-based vector store, NOT in EF entities
- **Indexing**: Rules are indexed by `rulesetId` - this is used as an index/filter for organizing and searching rules
- **Tool**: `RulesSearchTool` is registered with AI agents to allow them to search rules using natural language queries
- **Configuration**: Uses `WithSimpleVectorDb()` with a directory path for vector storage (path can be extracted from connection string format for backward compatibility)
- **References**: 
  - Blog post: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
  - Kernel Memory uses `ImportTextAsync()` to index rules and `AskAsync()` to search them

Database migrations and seed data

- **CRITICAL**: When modifying seed data in `JaimesDbContext.cs` (e.g., updating `SystemPrompt`, `NewGameInstructions`, or any `HasData()` values), you MUST create a new EF Core migration.
- Command to create migration: `dotnet ef migrations add <MigrationName> --project '.\JAIMES AF.Repositories\JAIMES AF.Repositories.csproj' --startup-project '.\JAIMES AF.ApiService\JAIMES AF.ApiService.csproj'`
- The warning about "potential data loss" when updating seed data is expected and safe to ignore - it's just EF Core being cautious about seed data updates.
- Migrations are automatically applied at application startup via the database initialization code.

Alternate filenames agents may check

- `.github/copilot-instructions.md`
- `AGENTS.md` (preferred for this repository)
- `README.md` and `CONTRIBUTING.md` for broader human-oriented orientation
