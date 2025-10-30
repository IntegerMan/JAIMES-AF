Development Guidelines and Best Practices

Purpose

This document captures the conventions, coding styles, testing approach, and general best practices used across the repository. Follow these to keep the codebase consistent and maintainable.

Layered Architecture

- Keep responsibilities separated:
 - `ApiService` for HTTP surface and request/response models only.
 - `Services` for application/business logic.
 - `Repositories` for data access and EF Core entities.

Service Design

- Expose interfaces for services (`I*Service`) and register concrete implementations in DI.
- Keep services small and single-responsibility. If a service grows large, split into smaller collaborators.
- Use mappers (e.g., `GameMapper`) for translating between entity, DTO, and response models.

Endpoints

- Endpoints should be minimal and delegate to services. Avoid business logic in endpoints.
- Define request and response models in `Requests/` and `Responses/` folders in `ApiService`.
- Validate inputs early and return clear error responses.

Data and EF Core

- Add DB schema changes via EF Core migrations in `Repositories/Migrations`.
- Keep `JaimesDbContext` as the single source of truth for DbSet properties.
- Seed data should be minimal and used only for dev/test convenience. Keep migration files checked in.

Testing

- Unit tests belong in `JAIMES AF.Tests`. Follow existing test patterns for endpoints and services.
- Use in-memory or test databases when testing repositories. Keep test data small and focused.
- Add tests alongside new features; prefer high coverage for services and key endpoints.

Code Style

- Use clear, expressive names for classes, methods, and parameters.
- Keep methods short (under ~40-60 lines) where possible.
- Favor immutability for DTOs where applicable.

Dependency Injection

- Register all application services in `ServiceCollectionExtensions` (or equivalent) and prefer constructor injection.
- Avoid Service Locator patterns.

Error Handling and Logging

- Use consistent error handling in services and endpoints. Return meaningful HTTP status codes from the API.
- Add logging to important decision points, errors, and unexpected behavior.

Documentation

- Add or update `TECHNICAL_OVERVIEW.md` and `DEVELOPMENT_GUIDELINES.md` when introducing large features or architecture changes.
- Document public APIs and major services with XML comments.

Versioning and Packages

- Keep NuGet package versions aligned across projects when possible.
- Target .NET9 for all projects; update csproj files if adding new projects.

Commits and PRs

- Write descriptive commit messages and PR descriptions.
- Keep PRs small and focused. Include tests and a short description of functional changes.

Onboarding and Orientation Files

To help AI assistants and new contributors quickly orient:
- Keep `TECHNICAL_OVERVIEW.md` and `DEVELOPMENT_GUIDELINES.md` up to date.
- Add README snippets in project roots if a project has unique conventions.

