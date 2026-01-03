# Technical Details

This document acts as a guide to the technical documentation of JAIMES AF. Each topic has been split into focused documents for easier navigation.

## Documentation Map

### [Getting Started](GETTING_STARTED.md)
The starting point for developers. Contains:
- Prerequisites and Setup
- Configuration details (Database, Secrets)
- Development Guidelines (Coding style, Architecture principles)

### [Architecture Overview](ARCHITECTURE.md)
High-level system design.
- **When to read**: First time looking at the project, or when making cross-cutting architectural changes.
- **Key topics**: Layered architecture, Data flow diagrams, Tech stack.

### [Agent Framework](AGENT_FRAMEWORK.md)
Deep dive into the AI integration.
- **When to read**: When working on creating new agents, adding tools, or debugging conversation state.
- **Key topics**: Tool usage, Thread persistence, Agent lifecycle.

### [Chat Streaming](CHAT_STREAMING.md)
Details of the real-time chat architecture.
- **When to read**: When modifying the chat UI or the SSE streaming endpoint.
- **Key topics**: Server-Sent Events (SSE) protocol, Client-side state machine.

### [Vector Search & RAG](VECTOR_SEARCH.md)
Explanation of the RAG (Retrieval-Augmented Generation) system.
- **When to read**: When working on document ingestion (PDFs/Rulesets) or search accuracy.
- **Key topics**: Qdrant integration, Document cracking/chunking pipeline.

### [Message Bus & Workers](MESSAGE_BUS.md)
Overview of asynchronous background processing.
- **When to read**: When adding new background tasks or debugging latency in analysis features.
- **Key topics**: LavinMQ, Sentiment Analysis, SignalR updates.

### [Database Schema](SCHEMA.md)
Database reference.
- **When to read**: When writing complex queries or modifying Entity Framework models.
- **Key topics**: ER Diagram, Entity relationships.

## Quick Reference

| Task | Primary Document |
|------|-----------------|
| Setup Environment | [Getting Started](GETTING_STARTED.md) |
| Understand Data Flow | [Architecture Overview](ARCHITECTURE.md) |
| Add a New AI Tool | [Agent Framework](AGENT_FRAMEWORK.md) |
| Fix Chat UI Bug | [Chat Streaming](CHAT_STREAMING.md) |
| Add a Background Job | [Message Bus & Workers](MESSAGE_BUS.md) |
| Modify Database | [Database Schema](SCHEMA.md) |

## Contributing to Documentation

When modifying the codebase:
1. **Update relevant docs** when behavior changes.
2. **Keep it focused** - each doc should cover one major topic.
3. **Use Mermaid** for diagrams over static images where possible.
