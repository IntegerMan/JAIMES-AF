# MongoDB to PostgreSQL Migration Summary

This document summarizes the migration from MongoDB to PostgreSQL with JSONB for document storage in the JAIMES AF project.

## Overview

All document storage (DocumentMetadata, CrackedDocument, and DocumentChunk) has been migrated from MongoDB to PostgreSQL using Entity Framework Core. This consolidates all data storage into a single PostgreSQL database.

## Changes Made

### 1. New EF Core Entities Created

Three new entities were created in `JAIMES AF.Repositories/Entities/`:

- **DocumentMetadata.cs** - Tracks scanned documents with file path, hash, and metadata
- **CrackedDocument.cs** - Stores extracted text content from documents (using TEXT column type)
- **DocumentChunk.cs** - Stores text chunks with foreign key to CrackedDocument

All entities use:
- Auto-incrementing integer primary keys
- Unique indexes on FilePath (DocumentMetadata, CrackedDocument) and ChunkId (DocumentChunk)
- Proper foreign key relationships with cascade delete

### 2. Database Context Updated

`JaimesDbContext.cs` was updated to include:
- DbSet properties for the three new entities
- Entity configurations with indexes and relationships
- Migration: `20251203120000_AddDocumentStorageTables.cs`

### 3. Services Updated

All document processing services were updated to use EF Core instead of MongoDB:

#### DocumentChangeDetectorService
- Now uses `JaimesDbContext` instead of `IMongoClient`
- Uses LINQ to Entities for queries
- Properly handles upserts using FirstOrDefaultAsync and SaveChangesAsync

#### DocumentCrackingService
- Replaced MongoDB operations with EF Core
- Document IDs are now integers instead of ObjectId strings
- Maintains backward compatibility by converting IDs to strings in messages

#### DocumentChunkingService
- Uses EF Core for storing chunks
- Properly handles relationships between CrackedDocument and DocumentChunk
- Maintains transaction consistency with SaveChangesAsync

#### DocumentEmbeddingService
- Updated to use EF Core for chunk updates
- Increments ProcessedChunkCount using EF Core

### 4. API Endpoints Updated

Three endpoints were updated:

- **ListDocumentsEndpoint** - Uses EF Core to query DocumentMetadata and CrackedDocuments
- **DeleteDocumentEndpoint** - Uses EF Core with cascade delete for chunks
- **RecrackDocumentEndpoint** - Queries DocumentMetadata using EF Core

### 5. Worker Configuration Updated

All worker `Program.cs` files were updated:

- **DocumentChangeDetector** - Added `AddJaimesRepositories()`, removed MongoDB client
- **DocumentCrackerWorker** - Added `AddJaimesRepositories()`, removed MongoDB client
- **DocumentChunking** - Added `AddJaimesRepositories()`, removed MongoDB client
- **DocumentEmbedding** - Added `AddJaimesRepositories()`, removed MongoDB client

### 6. AppHost Configuration Updated

`AppHost.cs` changes:
- Removed MongoDB server and database resource definitions
- Removed `.WithReference(mongoDb)` from all workers
- Removed `.WaitFor(mongo)` from all workers
- Added `.WithReference(postgresdb)` to all workers
- Added `.WaitFor(postgres)` to all workers
- Added comment noting MongoDB replacement

### 7. Package References Cleaned Up

Removed MongoDB package references from:
- `JAIMES AF.AppHost.csproj` - Removed `Aspire.Hosting.MongoDB`
- `JAIMES AF.ApiService.csproj` - Removed `Aspire.MongoDB.Driver`
- `JAIMES AF.Workers.DocumentChangeDetector.csproj` - Removed `Aspire.MongoDB.Driver`
- `JAIMES AF.Workers.DocumentCrackerWorker.csproj` - Removed `Aspire.MongoDB.Driver`
- `JAIMES AF.Workers.DocumentChunking.csproj` - Removed `Aspire.MongoDB.Driver`
- `JAIMES AF.Workers.DocumentEmbedding.csproj` - Removed `Aspire.MongoDB.Driver`
- `JAIMES AF.ServiceDefinitions.csproj` - Removed `MongoDB.Bson`

### 8. Legacy Models Marked as Obsolete

The old MongoDB models in `JAIMES AF.ServiceDefinitions` were marked as obsolete:
- `Models/DocumentMetadata.cs` - Marked obsolete, MongoDB attributes removed
- `Messages/CrackedDocument.cs` - Marked obsolete, MongoDB attributes removed
- `Messages/DocumentChunk.cs` - Marked obsolete, MongoDB attributes removed

These are kept for backward compatibility but should not be used for new code.

## Migration Notes

### Data Type Changes

1. **Document IDs**: Changed from MongoDB ObjectId (string) to PostgreSQL integer
   - Services convert between int and string for message compatibility
   - Messages still use string IDs for backward compatibility

2. **Content Storage**: Large text content uses PostgreSQL TEXT type instead of BSON

3. **Timestamps**: All DateTime values are stored as `timestamp with time zone` in PostgreSQL

### Index Strategy

- **FilePath** columns have unique indexes for fast lookups
- **ChunkId** has a unique index for cross-referencing with Qdrant
- **DocumentId** in DocumentChunk has a non-unique index for efficient queries

### Cascade Behavior

- Deleting a CrackedDocument cascades to delete all associated DocumentChunks
- This maintains referential integrity automatically

## Testing Requirements

Before deploying, ensure:

1. **Build the solution**: `dotnet build`
2. **Run migrations**: Migrations will run automatically on application startup
3. **Test document scanning**: Verify DocumentChangeDetector can scan and enqueue documents
4. **Test document cracking**: Verify DocumentCrackerWorker can extract text
5. **Test chunking**: Verify DocumentChunking can create and store chunks
6. **Test embedding**: Verify DocumentEmbedding can generate and store embeddings
7. **Test API endpoints**: Verify `/documents`, `/documents/delete`, and `/documents/recrack` work correctly

## Test File Updates Needed

The following test files will need updates (not completed in this migration due to environment limitations):

- `JAIMES AF.Tests/Workers/DocumentChangeDetectorServiceTests.cs` - Replace Mongo2Go with in-memory EF Core
- `JAIMES AF.Tests/Workers/DocumentCrackingServiceTests.cs` - Replace Mongo2Go with in-memory EF Core
- `JAIMES AF.Tests/Workers/DocumentChunkingServiceTests.cs` - Replace Mongo2Go with in-memory EF Core
- `JAIMES AF.Tests/Workers/DocumentEmbeddingServiceTests.cs` - Replace Mongo2Go with in-memory EF Core
- `JAIMES AF.Tests/TestUtilities/MongoTestRunner.cs` - Can be removed or replaced with EF Core test utilities

Test updates should:
1. Remove `Mongo2Go` package reference from test project
2. Use `DbContextOptionsBuilder<JaimesDbContext>().UseInMemoryDatabase()` for tests
3. Update test setup to create in-memory database instances
4. Update assertions to work with integer IDs instead of ObjectId strings

## Benefits of This Migration

1. **Unified Data Storage**: All data now in PostgreSQL, simplifying deployment and backup
2. **Better Tooling**: Can use standard PostgreSQL tools for monitoring and administration
3. **Improved Performance**: PostgreSQL indexes and query optimization
4. **Referential Integrity**: Foreign key constraints ensure data consistency
5. **Simpler Infrastructure**: One less database system to manage
6. **Cost Reduction**: No need for separate MongoDB hosting

## Rollback Plan

If issues arise:
1. The old MongoDB models are still present (marked obsolete)
2. MongoDB packages can be re-added to Directory.Packages.props
3. Services can be reverted to use MongoDB by restoring from git history
4. Data would need to be manually migrated back to MongoDB if necessary

## Next Steps

1. Build and test the solution locally
2. Update test files to use in-memory EF Core database
3. Run all tests to ensure functionality
4. Deploy to development environment
5. Verify document processing pipeline works end-to-end
6. Monitor for any issues
7. Once stable, remove obsolete MongoDB models and packages completely
