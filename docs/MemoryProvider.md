# Custom MemoryProvider Implementation for Agent Framework

## Overview

This document describes the custom `MemoryProvider` implementation for the JAIMES AF Agent Framework integration. The MemoryProvider follows Microsoft's Agent Framework pattern for managing long-term conversation memory and integrates seamlessly with Entity Framework Core and PostgreSQL.

## Architecture

### Components

1. **IMemoryProvider** (`JAIMES AF.ServiceDefinitions/Services/IMemoryProvider.cs`)
   - Interface defining the contract for memory persistence
   - Provides methods for loading threads, saving conversations, and retrieving history

2. **EntityFrameworkMemoryProvider** (`JAIMES AF.Agents/Services/EntityFrameworkMemoryProvider.cs`)
   - Concrete implementation using Entity Framework Core
   - Integrates with existing PostgreSQL database schema
   - Uses `IDbContextFactory<JaimesDbContext>` for database access

3. **GameAwareAgent** (`JAIMES AF.ApiService/Agents/GameAwareAgent.cs`)
   - Updated to use IMemoryProvider instead of direct ChatHistoryService calls
   - Resolves IMemoryProvider from service provider (scoped lifetime)

## Key Features

### 1. Thread State Management

The MemoryProvider manages `AgentThread` objects which contain the conversation state:

```csharp
Task<AgentThread> LoadThreadAsync(Guid gameId, AIAgent agent, CancellationToken cancellationToken = default);
```

- Loads the most recent thread from the `ChatHistory` table
- Deserializes thread JSON using the agent's deserializer
- Returns a new thread if no history exists

### 2. Conversation Persistence

```csharp
Task SaveConversationAsync(
    Guid gameId,
    string playerId,
    ChatMessage? userMessage,
    IEnumerable<ChatMessage> assistantMessages,
    AgentThread thread,
    CancellationToken cancellationToken = default);
```

- Saves user and assistant messages to the `Message` table
- Serializes and stores thread state in the `ChatHistory` table
- Links messages to chat history for audit trail
- Maintains conversation continuity across requests

### 3. History Retrieval

```csharp
Task<IEnumerable<ChatMessage>> GetConversationHistoryAsync(Guid gameId, CancellationToken cancellationToken = default);
```

- Retrieves all messages for a game in chronological order
- Converts Entity Framework entities to `ChatMessage` objects
- Useful for displaying conversation history or rebuilding context

## Database Schema Integration

The MemoryProvider integrates with the existing database schema:

### Message Entity
- `Id`: Auto-incrementing primary key
- `GameId`: Foreign key to Game
- `Text`: Message content
- `PlayerId`: Nullable - identifies player (null = Game Master)
- `CreatedAt`: Timestamp
- `ChatHistoryId`: Optional link to ChatHistory

### ChatHistory Entity
- `Id`: Unique identifier
- `GameId`: Foreign key to Game
- `ThreadJson`: Serialized AgentThread state
- `CreatedAt`: Timestamp
- `PreviousHistoryId`: Links to previous history (audit trail)
- `MessageId`: Optional link to last message in this thread state

### Game Entity
- `MostRecentHistoryId`: Points to the current thread state

## Dependency Injection

The MemoryProvider is registered as a scoped service:

```csharp
builder.Services.AddScoped<IMemoryProvider, EntityFrameworkMemoryProvider>();
```

### Lifetime Considerations

- **IMemoryProvider**: Scoped - one instance per request
- **GameAwareAgent**: Singleton - resolves IMemoryProvider from service provider when needed
- **IDbContextFactory**: Singleton - creates scoped DbContext instances

This pattern ensures:
- Thread safety across concurrent requests
- Proper disposal of database connections
- Isolation of database operations per request

## Usage Example

### Loading a Thread

```csharp
IMemoryProvider memoryProvider = serviceProvider.GetRequiredService<IMemoryProvider>();
AIAgent agent = CreateAgent();
AgentThread thread = await memoryProvider.LoadThreadAsync(gameId, agent, cancellationToken);
```

### Saving a Conversation

```csharp
ChatMessage userMessage = new(ChatRole.User, "What's my character's name?");
IEnumerable<ChatMessage> assistantMessages = new[]
{
    new ChatMessage(ChatRole.Assistant, "Your character is Emcee, a brave adventurer!")
};

await memoryProvider.SaveConversationAsync(
    gameId,
    playerId,
    userMessage,
    assistantMessages,
    thread,
    cancellationToken);
```

## Testing

Comprehensive tests are provided in `EntityFrameworkMemoryProviderTests.cs`:

- **LoadThreadAsync_ReturnsNewThread_WhenNoHistoryExists**: Verifies new thread creation
- **LoadThreadAsync_ReturnsDeserializedThread_WhenHistoryExists**: Tests thread deserialization
- **SaveConversationAsync_SavesMessagesAndThread**: Validates persistence
- **SaveConversationAsync_HandlesNullUserMessage**: Tests edge case handling
- **GetConversationHistoryAsync_ReturnsMessagesInOrder**: Verifies chronological ordering
- **GetConversationHistoryAsync_ReturnsEmptyForNonExistentGame**: Tests non-existent game handling

## Benefits

1. **Separation of Concerns**: Memory management is abstracted from agent logic
2. **Testability**: Interface allows easy mocking in tests
3. **Consistency**: Follows Agent Framework patterns and conventions
4. **Flexibility**: Easy to swap implementations (e.g., Redis, Cosmos DB)
5. **Maintainability**: Centralized memory management logic
6. **Observability**: Comprehensive logging at all levels

## Future Enhancements

Potential improvements for future iterations:

1. **Caching**: Add Redis caching layer for frequently accessed threads
2. **Compression**: Compress thread JSON for large conversations
3. **Archival**: Move old conversations to cold storage
4. **Analytics**: Track conversation patterns and agent performance
5. **Multi-Agent**: Support multiple agents per game with separate threads
6. **Snapshots**: Create conversation snapshots at key points

## References

- [Microsoft Agent Framework Memory Documentation](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/memory?pivots=programming-language-csharp)
- [Entity Framework Core DbContext Factory](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#using-a-dbcontext-factory-eg-for-blazor)
- [Dependency Injection Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
