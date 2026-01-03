# Agent Framework Integration

This document explains the Microsoft.Extensions.AI Agent Framework integration, including how agents are created, how they use tools, and how conversation history is managed.

## Overview

JAIMES AF uses the **Microsoft.Extensions.AI Agent Framework** to power its game master AI capabilities. The framework provides:

- **Unified Chat Interface** - Support for multiple LLM providers.
- **Stateful Agents** - Agents that maintain conversation context across messages.
- **Tool/Function Calling** - Agents can invoke tools to retrieve information dynamically.
- **Streaming Support** - Token-by-token streaming for responsive UX.
- **Thread Management** - Conversation history persistence and restoration.

## Agent Architecture

### Layered Approach

The system uses a decorator pattern to add capabilities to the base chat client:

```mermaid
graph TD
    A[GameAwareAgent (Game Context)] --> B[Agent (Thread Management)]
    B --> C[InstrumentedChatClient (Logging)]
    C --> D[OpenTelemetryChatClient (Tracing)]
    D --> E[FunctionInvokingChatClient (Tools)]
    E --> F[IChatClient (Provider: OpenAI/Ollama)]
```

### Key Components

- **GameAwareAgent**: Validates game state, manages persistence, and coordinates with the message bus.
- **Agent (Framework)**: Maintains conversation threads and orchestrates tool execution.
- **Chat Client**: The low-level interface to the LLM provider, wrapped with instrumentation and function invocation capabilities.

## Agent Tools

Agents have access to several tools that retrieve contextual information during conversations:

1. **PlayerInfoTool**: Retrieves player character details (name, description) to personalize responses.
2. **RulesSearchTool**: Queries the vector database for relevant ruleset content (RAG).
3. **ConversationSearchTool**: Searches past conversation messages to recall context.
4. **PlayerSentimentTool**: Checks the player's recent sentiment trend to adjust tone.

### How Tools Work

1. **Registration**: Tools are defined with descriptions that tell the LLM *when* to use them.
2. **Invocation**: If the LLM requests a tool call, the framework executes the underlying C# code automatically.
3. **Tracking**: Every tool call is logged to the database with its input and output, enabling analysis of agent behavior.

## Conversation Threads

### What is a Thread?

A **thread** represents the full conversation history for a game session, including:
- User and Assistant messages.
- System prompts (game instructions).
- Tool call results.

### Persistence Strategy

Threads are serialized to **JSON** and stored in the database. This allows:
- **Resumability**: A player can leave and return to the game seamlessly.
- **Context Preservation**: The agent "remembers" everything that has happened in the session.
- **History Management**: Older messages can be summarized or pruned if the context window gets too full (future optimization).

## Agent Creation Flow

Each HTTP request creates a fresh agent instance to ensure isolation and data freshness:

1. **Load Context**: Fetch the Game, Ruleset, and Player data from the database.
2. **Hydrate Tools**: Create tool instances scoped to the specific game ID.
3. **Load Thread**: Retrieve the existing conversation history.
4. **Execute**: Pass the user's new message to the agent and stream the response.

## Integration Points

- **Chat Streaming**: Agents stream responses token-by-token via Server-Sent Events (SSE).
- **Vector Search**: Tools use the Qdrant database to find relevant semantic information.
- **Message Bus**: After a response is complete, the `GameAwareAgent` publishes events to trigger background analysis (sentiment, metrics).

## Related Documentation

- [Chat Streaming Architecture](CHAT_STREAMING.md) - How agents stream responses
- [Vector Search & RAG](VECTOR_SEARCH.md) - Tool implementation details
- [Architecture Overview](ARCHITECTURE.md) - High-level system design
- [Database Schema](SCHEMA.md) - Message and thread storage
