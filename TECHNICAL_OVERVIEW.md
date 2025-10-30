Project Technical Overview

This repository is a .NET9 multi-project solution implementing a lightweight tabletop role-playing game backend and Blazor web client. The codebase follows a layered architecture with clear separation of concerns and conventions. Use this document to quickly understand the high-level structure and where to look when making changes.

Projects

- `JAIMES AF.ApiService` - Minimal API project exposing HTTP endpoints. Endpoints are in `Endpoints/` and use request/response DTOs in `Requests/` and `Responses/`. `Program.cs` wires up DI and endpoint registration.
- `JAIMES AF.Services` (a.k.a ServiceLayer) - Business logic and application services. Services live in `Services/` and expose interfaces under the same namespace (e.g. `IGameService`). Mapper classes convert between repository entities and DTOs.
- `JAIMES AF.Repositories` - Persistence layer using EF Core. Contains `JaimesDbContext`, entity classes (`Entities/`), and migrations (`Migrations/`). Data access is provided through repository classes and registered via `RepositoryServiceCollectionExtensions`.
- `JAIMES AF.Web` - Blazor client project (component-based UI). Components live under `Components/Pages/` and client helper classes (e.g., `WeatherApiClient`) live at the project root.
- `JAIMES AF.AppHost` - Host utilities for running apps or tests.
- `JAIMES AF.ServiceDefaults` - Shared configuration defaults and extension helpers.
- `JAIMES AF.Tests` - Unit and integration tests (contains endpoint tests, repository tests, and service tests). Tests exercise both service layer and API endpoints.

Key Patterns and Conventions

- Endpoints are lightweight; heavy business logic belongs in service layer.
- Services expose interfaces (`I*Service`) and concrete implementations in `Services/`.
- DTOs (e.g., `GameDto`, `ScenarioDto`) are defined in the `Services.Models` namespace.
- API input/output types are `Requests/` and `Responses/` in the `ApiService` project.
- Mappers (e.g. `GameMapper`, `ScenarioMapper`) convert between entities and DTOs/Response models.
- Tests use an in-memory or test database via migration/DbContext helpers in `Repositories`.

Entry Points

- API: `JAIMES AF.ApiService/Program.cs` registers services and endpoints.
- Web: `JAIMES AF.Web/Program.cs` is the Blazor client startup.

Build and test

- Build solution: `dotnet build` (root directory)
- Run tests: `dotnet test` (root directory)

Where to make common changes

- Add new HTTP endpoints: add a new file under `JAIMES AF.ApiService/Endpoints`, add `Request` and `Response` types under `Requests/` and `Responses/`, and implement service methods in `JAIMES AF.Services/Services`.
- Add new database entities: add entity in `JAIMES AF.Repositories/Entities`, add DbSet to `JaimesDbContext`, add and run EF Core migration.
- Add business logic: extend or add a service in `JAIMES AF.Services` and update DI registration in `JAIMES AF.Services/ServiceCollectionExtensions.cs`.

Notes

- Target framework is .NET9. Keep packages and target TFMs aligned across projects.
- Prefer small, well-tested methods. The tests in `JAIMES AF.Tests` are a good pattern to follow for coverage and structure.
