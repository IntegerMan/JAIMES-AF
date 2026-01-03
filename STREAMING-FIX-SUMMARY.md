# Streaming Chat Fix Summary

## Problem Identified

The streaming chat was not working because `MapAGUI` (from Microsoft.Agents.AI.AGUI) was **buffering the entire response** before sending it to the client. This resulted in:

1. **No true streaming** - the client received everything at once after 52ms
2. **Complex ID matching** - the client had to use SignalR notifications to match streamed messages to database IDs
3. **No visibility** - the Ollama streaming loop never executed because the stream wasn't being properly consumed

### Evidence from Logs

**Server logs showed**:
- ✅ `🤖 Agent streaming run started` - Agent was called correctly
- ✅ `💬 Chat client streaming invocation started` - Chat client was invoked
- ❌ **Missing**: No "Ollama SSE Line" logs - the stream was never consumed

**Client logs showed**:
- ❌ Response received in **52.3535ms** - buffered, not streamed
- ❌ Only ONE "Stream update" with empty text

## Solution Implemented

### 1. **Created Custom Streaming Endpoint**
**File**: `JAIMES AF.ApiService/Endpoints/StreamingChatEndpoint.cs`

- Properly handles **Server-Sent Events (SSE)**
- Flushes each text delta immediately (true streaming)
- Sends message IDs after persistence (no more ID matching needed)
- Disables buffering with `X-Accel-Buffering: no` header

**Endpoint**: `POST /games/{gameId}/chat`

### 2. **Updated GameAwareAgent to Return Message IDs**
**File**: `JAIMES AF.ApiService/Agents/GameAwareAgent.cs`

Modified `PersistGameStateAsync` to return:
```csharp
(int? UserMessageId, List<int> AssistantMessageIds)
```

After streaming completes, sends a final `ChatRole.System` message with:
```json
{
  "Type": "MessagePersisted",
  "UserMessageId": 123,
  "AssistantMessageIds": [124, 125]
}
```

### 3. **Fixed Route Values for FastEndpoints**
**File**: `JAIMES AF.ApiService/Endpoints/StreamingChatEndpoint.cs`

**Critical fix**: FastEndpoints doesn't automatically populate `HttpContext.Request.RouteValues["gameId"]` from the route template, but `GameAwareAgent` depends on it to extract the gameId.

Added explicit route value population at the start of `HandleAsync`:
```csharp
// CRITICAL: Set the gameId in route values so GameAwareAgent can extract it from HttpContext
// FastEndpoints may not populate this automatically, so we set it explicitly
HttpContext.Request.RouteValues["gameId"] = req.GameId.ToString();
```

This ensures `GameAwareAgent.GetOrCreateGameAgentAsync` can successfully extract the gameId from the HttpContext.

### 4. **Disabled AGUI Middleware**
**File**: `JAIMES AF.ApiService/Program.cs`

**Critical**: Commented out BOTH `AddAGUI()` (line 35) and `MapAGUI` (line 188).

The `AddAGUI()` call was the hidden culprit - it registers AGUI middleware that **automatically creates routes** for any `AIAgent` registered in dependency injection. Since `GameAwareAgent` is registered as a singleton, AGUI was intercepting the `/games/{gameId:guid}/chat` route BEFORE FastEndpoints could handle it, bypassing our custom `StreamingChatEndpoint` entirely.

```csharp
// DISABLED: AddAGUI() was causing AGUI middleware to intercept our custom streaming endpoint
// AGUI automatically creates routes for any AIAgent in DI, bypassing FastEndpoints
// builder.Services.AddAGUI();
```

## SSE Event Format

The new endpoint sends these SSE events:

### `delta` - Text streaming events
```json
{
  "messageId": "default",
  "textDelta": "Hello",
  "role": "Assistant",
  "authorName": "Game Master"
}
```

### `persisted` - Message IDs after persistence
```json
{
  "Type": "MessagePersisted",
  "UserMessageId": 123,
  "AssistantMessageIds": [124]
}
```

### `done` - Stream complete
```json
{
  "message": "Stream complete"
}
```

### `error` - Error occurred
```json
{
  "error": "Error message",
  "type": "ExceptionType"
}
```

## Client-Side Changes Completed ✅

The client has been updated to consume SSE instead of using the AGUI client. Here's what was changed:

### 1. Removed AGUI Client Dependencies

**Removed**:
- `Microsoft.Agents.AI` and `Microsoft.Agents.AI.AGUI` using statements
- `_agent` field (AIAgent instance)
- `_pendingMessageAgentInfo` dictionary for temporary ID tracking
- AGUI client initialization code in `LoadGameAsync`

### 2. Replaced SendMessagePrivateAsync with SSE Implementation

**New implementation**:
- Uses `HttpClient.SendAsync` with `HttpCompletionOption.ResponseHeadersRead`
- Reads SSE stream line-by-line using `StreamReader`
- Parses `event:` and `data:` lines according to SSE specification
- Handles four event types:
  - `delta` - Streaming text updates
  - `persisted` - Database message IDs after persistence
  - `done` - Stream completion
  - `error` - Error events

### 3. Simplified ID Handling

**Removed**:
- Complex SignalR-based content matching (100+ lines)
- `_pendingMessageAgentInfo` tracking dictionary
- Message content comparison logic

**Simplified to**:
- Receive IDs directly from `persisted` event
- Map IDs to message indexes immediately
- No more content matching or race conditions

## Benefits

1. ✅ **True streaming** - see text appearing character by character
2. ✅ **Immediate message IDs** - no more SignalR matching
3. ✅ **Simpler code** - remove complex ID tracking logic
4. ✅ **Better performance** - no buffering delays
5. ✅ **Standard protocol** - uses SSE (EventSource API)

## Testing Steps

### Ready to Test! 🧪

Both server and client changes are complete and building successfully. To test:

1. **Start the AppHost**:
   ```bash
   dotnet run --project "c:\Dev\JAIMES-AF\JAIMES AF.AppHost\JAIMES AF.AppHost.csproj"
   ```

2. **Navigate to a game** in the web UI

3. **Send a chat message** and observe:
   - ✅ Text should appear **character by character** (true streaming)
   - ✅ No more 52ms buffered response - you'll see actual streaming
   - ✅ Message IDs appear immediately in the `persisted` event
   - ✅ Check browser DevTools Network tab - you should see `text/event-stream` content type
   - ✅ Server logs should show "Ollama SSE Line" entries (the stream is being consumed!)

4. **Check the logs**:
   - **Server**: Look for "Ollama SSE Line" and "Ollama stream marked as done"
   - **Client**: Look for "Received persisted IDs - User: X, Assistant: Y"
   - **Browser Console**: Should show text deltas appearing incrementally

## Troubleshooting Journey

### Issue 1: Client Not Receiving Response Headers
**Symptom**: Client hung waiting for response headers, never received them

**Diagnosis**: The SSE endpoint wasn't starting the HTTP response before the streaming loop

**Fix**: Added explicit response start with flush in `StreamingChatEndpoint.cs`:
```csharp
HttpContext.Response.StatusCode = 200;
await HttpContext.Response.Body.FlushAsync(ct);
```

### Issue 2: Stream Closes Immediately After Headers (24ms)
**Symptom**:
- Client receives headers in 23ms but stream ends after 24ms
- No streaming content sent to client
- Server logs show Ollama request initiated but no "Ollama Stream opened" logs

**Diagnosis**:
The `GameAwareAgent.GetOrCreateGameAgentAsync` requires `HttpContext.Request.RouteValues["gameId"]` to extract the gameId (line 236-240). However, FastEndpoints doesn't automatically populate route values from the route template like standard ASP.NET Core routing does.

When the route value is missing, this code throws an `ArgumentException`:
```csharp
string? gameIdStr = context.Request.RouteValues["gameId"]?.ToString();
if (!Guid.TryParse(gameIdStr, out Guid gameId))
{
    _logger.LogError("Invalid game ID in route: {GameIdStr}", gameIdStr);
    throw new ArgumentException("Invalid game ID in route");
}
```

The exception causes the async enumerable to exit immediately, closing the stream.

**Fix**: Explicitly populate route values at the start of `StreamingChatEndpoint.HandleAsync`:
```csharp
// CRITICAL: Set the gameId in route values so GameAwareAgent can extract it from HttpContext
// FastEndpoints may not populate this automatically, so we set it explicitly
HttpContext.Request.RouteValues["gameId"] = req.GameId.ToString();
```

### Issue 3: AGUI Middleware Intercepting Custom Endpoint
**Symptom**:
- Custom `StreamingChatEndpoint` logging never appears in server logs
- Server logs show `GameAwareAgent` being called directly
- No "StreamingChatEndpoint: Starting chat" or "StreamingChatEndpoint: About to call" messages
- Stream still closes after 26ms

**Diagnosis**:
The `AddAGUI()` call in `Program.cs` (line 33) registers AGUI middleware that automatically discovers and creates routes for any `AIAgent` registered in dependency injection. Since `GameAwareAgent` is registered as a singleton, AGUI was automatically creating a `/games/{gameId:guid}/chat` route and handling all requests to it, completely bypassing the custom FastEndpoints `StreamingChatEndpoint`.

AGUI middleware runs early in the pipeline and intercepts matching routes before FastEndpoints gets a chance to process them.

**Fix**: Comment out the `AddAGUI()` call in `Program.cs`:
```csharp
// DISABLED: AddAGUI() was causing AGUI middleware to intercept our custom streaming endpoint
// AGUI automatically creates routes for any AIAgent in DI, bypassing FastEndpoints
// builder.Services.AddAGUI();
```

## AGUI Cleanup Completed ✅

All AGUI dependencies and code have been completely removed:

**Packages Removed**:
- ❌ `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` from ApiService
- ❌ `Microsoft.Agents.AI.AGUI` from Web project
- ✅ Kept `Microsoft.Agents.AI` (core Agent Framework)

**Code Removed**:
- Removed `using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;` from `GlobalUsings.cs`
- Removed `using Microsoft.Agents.AI.AGUI;` from Web `Program.cs` and test file
- Removed AGUI HttpClient registration from Web `Program.cs`
- Removed `AddAGUI()` and `MapAGUI()` calls from ApiService `Program.cs`
- Updated comments to reference `StreamingChatEndpoint` instead of MapAGUI

## Status

✅ **Server-side changes complete** - Custom SSE endpoint with route value fix and AGUI removed
✅ **Client-side changes complete** - SSE parsing and ID handling
✅ **AGUI cleanup complete** - All AGUI packages and code removed
✅ **Build successful** - All projects compile without errors (ApiService, Web, Tests, AppHost)
🧪 **Ready for testing** - All critical fixes applied
