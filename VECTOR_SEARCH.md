# Vector Search & RAG

This document details the vector search and Retrieval-Augmented Generation (RAG) implementation using Qdrant.

## Overview

JAIMES AF uses **Qdrant** for vector storage and semantic search. This allows AI agents to find and "recall" information that doesn't fit in their immediate context window.

There are two primary search domains:
1. **Ruleset Content**: Static game rules (PDFs, reference docs) indexed for retrieval.
2. **Conversation History**: Dynamic chat logs indexed for long-term memory.

## Architecture

The document processing pipeline converts raw text into mathematical vectors that can be searched by meaning.

```mermaid
graph TD
    Sources[Document Sources] --> Detect[Change Detection]
    Detect --> Crack[Text Extraction (Cracking)]
    Crack --> Chunk[Text Chunking]
    Chunk --> Embed[Embedding Generation]
    Embed --> Qdrant[(Qdrant Vector DB)]
    Embed --> SQL[(PostgreSQL Metadata)]
    
    Query[AI Agent] --> Search[Hybrid Search]
    Search --> Qdrant
    Qdrant --> Results[Relevant Context]
```

## Data Organizations (Collections)

### 1. Rulesets
Stores chunks of rulebook content.
- **Vectors**: 768 dimensions (default).
- **Metadata**: Source filename, page number, chunk ID.
- **Usage**: Used by `RulesSearchTool` to answer questions like "How does a Fireball spell work?".

### 2. Conversations
Stores individual chat messages.
- **Vectors**: 768 dimensions.
- **Metadata**: Game ID, Role (User/Assistant), Timestamp.
- **Usage**: Used by `ConversationSearchTool` to recall past events like "What was the NPC's name we met yesterday?".

## Processing Pipeline Steps

### 1. Change Detection
A background service monitors the file system or external sources for new or modified documents. It keeps a hash of files to avoid redundant processing.

### 2. Document Cracking
Files (PDF, Markdown, Text) are "cracked" to extract raw text and metadata (like page counts). This text is normalized for processing.

### 3. Chunking
Raw text is too large for embedding models. It is split into smaller, overlapping "chunks" (e.g., 500 tokens). Overlap ensures that context isn't lost at the edges of a chunk.

### 4. Embedding
An AI model (like `nomic-embed-text` or OpenAI's `text-embedding-3-small`) converts each chunk into a vector. These vectors are upserted into Qdrant, while the original text is stored in PostgreSQL for display.

## Search Strategy

When an agent searches:
1. The search query ("fireball damage") is converted into a vector using the same model.
2. Qdrant performs a Cosine Similarity search to find the nearest vectors.
3. Results are filtered by metadata (e.g., only search the current game's ruleset).
4. The top matches (RAG context) are returned to the agent to help it generate an accurate answer.

## Related Documentation

- [Architecture Overview](ARCHITECTURE.md) - Background pipeline architecture
- [Agent Framework Integration](AGENT_FRAMEWORK.md) - How agents use search tools
- [Message Bus & Workers](MESSAGE_BUS.md) - Worker coordination
- [Database Schema](SCHEMA.md) - Metadata storage
