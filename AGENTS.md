Agents Orientation

Purpose

This file provides minimal, machine-oriented hints and common symbols to help AI assistants (Copilot, GitHub Copilot, and other agents) understand the repository structure and naming conventions.

Key service interfaces and locations

- `IGameService` -> `JAIMES AF.Services/Services/IGameService.cs`
- `IScenariosService` -> `JAIMES AF.Services/Services/IScenariosService.cs`
- `IPlayersService` -> `JAIMES AF.Services/Services/IPlayersService.cs`
- `ChatService` -> `JAIMES AF.Services/Services/ChatService.cs`

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

Development notes

- Target: `.NET9`
- DI registration: `ServiceCollectionExtensions.cs` in each layer registers services and repositories.
- Keep endpoints thin and delegate to services.

Alternate filenames agents may check

- `.github/copilot-instructions.md`
- `AGENTS.md` (preferred for this repository)
- `README.md` and `CONTRIBUTING.md` for broader human-oriented orientation
