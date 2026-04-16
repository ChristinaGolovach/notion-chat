# Notion AI Chatbot — Development Plan

## Backend Project Structure

```
NotionAgent.Api/
├── Program.cs
├── appsettings.json
├── Controllers/
│   └── ChatController.cs
├── Services/
│   ├── AgentOrchestrator.cs
│   ├── ClaudeApiClient.cs
│   ├── McpToolRouter.cs
│   ├── ConversationManager.cs
│   └── PromptBuilder.cs
└── Models/
    ├── ChatMessage.cs
    ├── ContentBlock.cs
    ├── ToolDefinition.cs
    ├── ClaudeRequest.cs
    ├── ClaudeResponse.cs
    └── Conversation.cs
```

---


## Phase 1: Project Setup & Scaffolding

| # | Task | Details |
|---|------|---------|
| 1.1 | Create .NET Web API project | `dotnet new webapi -n NotionAgent.Api` |
| 1.2 | Install NuGet packages | `ModelContextProtocol`, `Anthropic SDK` or `HttpClient`-based |
| 1.3 | Create Angular project | `ng new notion-agent-ui` |
| 1.4 | Configure solution structure | Set up `appsettings.json` with `Claude:ApiKey`, `Notion:ApiKey`, CORS policy for Angular dev server |
| 1.5 | Verify both projects run | API on `https://localhost:5001`, Angular on `http://localhost:4200` |

---

## Phase 2: MCP Tool Router (Notion Connection)

| # | Task | Details |
|---|------|---------|
| 2.1 | Install Notion MCP server | Ensure `npx @notionhq/notion-mcp-server` runs locally, configure Notion integration token |
| 2.2 | Create `McpToolRouter` class | Spawn MCP server as child process via `StdioClientTransport` |
| 2.3 | Implement `InitializeAsync()` | Connect to MCP server, handle startup errors |
| 2.4 | Implement `GetToolDefinitionsAsync()` | Call `ListToolsAsync()`, map to Claude tool format |
| 2.5 | Implement `ExecuteToolAsync()` | Call `CallToolAsync()`, return serialized result |
| 2.6 | Implement `IAsyncDisposable` | Clean shutdown of child process |
| 2.7 | **Test manually** | Write a minimal console test: init → list tools → call `search` → print result. Confirm Notion data comes back |

---

## Phase 3: Claude API Client

| # | Task | Details |
|---|------|---------|
| 3.1 | Create `Models/` | `ChatMessage`, `ContentBlock`, `ToolDefinition`, `ClaudeResponse`, `ClaudeRequest` |
| 3.2 | Create `ClaudeApiClient` | `HttpClient` wrapper with API key header, anthropic-version header |
| 3.3 | Implement `SendMessageAsync()` | Non-streaming POST to `v1/messages`, deserialize response, handle `tool_use` and `text` content blocks |
| 3.4 | **Test manually** | Send a simple message (no tools) → verify Claude responds. Then send with tool definitions → verify Claude returns `tool_use` blocks |

---

## Phase 4: Prompt Builder & Conversation Manager

| # | Task | Details |
|---|------|---------|
| 4.1 | Create `PromptBuilder` | Build system prompt with rules: use Notion tools, never fabricate, cite sources |
| 4.2 | Create `Conversation` model | `Id`, `List<ChatMessage> Messages`, `CreatedAt` |
| 4.3 | Create `ConversationManager` | `ConcurrentDictionary<string, Conversation>`, methods: `GetOrCreate`, `AddUserMessage`, `AddAssistantMessage`, `AddToolResult` |
| 4.4 | Implement message trimming | Drop oldest messages when conversation exceeds a token/message count threshold |

---

## Phase 5: Agent Orchestrator (The Core)

| # | Task | Details |
|---|------|---------|
| 5.1 | Create `AgentOrchestrator` | Inject all four services above |
| 5.2 | Implement `InitializeAsync()` | Init MCP router, cache tool definitions |
| 5.3 | Implement `ProcessMessageAsync()` | The agentic loop: send to Claude → check for tool_use → execute tools → loop or return final text |
| 5.4 | Add max iteration guard | `maxIterations = 10`, return error if exceeded |
| 5.5 | Add error handling for tool calls | Catch MCP failures, return error text to Claude so it can inform the user gracefully |
| 5.6 | **Test end-to-end (no UI)** | Call `ProcessMessageAsync("What pages do I have?")` from a test/console → verify full loop works |

**Critical milestone — the entire backend works before you touch the UI.**

---

## Phase 6: API Controller

| # | Task | Details |
|---|------|---------|
| 6.1 | Create `ChatController` | Two endpoints (see API Contract above) |
| 6.2 | `POST /api/chat/conversations` | Creates a conversation, returns `{ conversationId }` |
| 6.3 | `POST /api/chat/conversations/{id}/messages` | Accepts `{ message: string }`, calls orchestrator, returns `{ reply: string }` |
| 6.4 | Register DI | Register all services in `Program.cs`, call `AgentOrchestrator.InitializeAsync()` on app startup |
| 6.5 | Configure CORS | Allow Angular dev origin (`http://localhost:4200`) |
| 6.6 | **Test with Postman** | Create conversation → send message → verify full answer returned |

---

## Phase 7: Angular UI — Chat Interface

| # | Task | Details |
|---|------|---------|
| 7.1 | Create `ChatService` | `HttpClient` wrapper: `createConversation()`, `sendMessage(conversationId, message): Observable<ChatResponse>` |
| 7.2 | Create `ChatComponent` | Input box + message list. Displays user and assistant messages |
| 7.3 | Wire up request/response | On submit: add user message to list → call `sendMessage()` → add response to list |
| 7.4 | Add loading state | Show spinner/indicator while waiting for response. Disable input during request |
| 7.5 | Handle errors | Display API errors (timeout, 500, network) with user-friendly message |
| 7.6 | Basic styling | Clean chat layout — role indicators, auto-scroll to bottom, input at bottom |
| 7.7 | New chat button | Calls `createConversation()`, clears message list |

---

## Phase 8: Polish & Hardening

| # | Task | Details |
|---|------|---------|
| 8.1 | Handle MCP server crashes | Detect child process exit, auto-restart or return error |
| 8.2 | Add request cancellation | User can cancel in-flight request (Angular `unsubscribe` → `CancellationToken` on backend) |
| 8.3 | Tune system prompt | Iterate based on real usage — adjust Claude's behavior |
| 8.4 | Add request timeout | Set `HttpClient` timeout on Angular side (e.g. 60s), matching backend timeout for Claude + MCP calls |
| 8.5 | Test edge cases | Empty Notion results, very long responses, rapid sequential messages, tool timeout |

---

## Execution Order & Dependencies

```
Phase 1 (Setup)
    │
    ├──► Phase 2 (MCP Router)──────┐
    │                               │
    ├──► Phase 3 (Claude Client)───┤  ← These 3 can be built in parallel
    │                               │
    └──► Phase 4 (Prompt + Conv)───┤
                                    │
                              Phase 5 (Orchestrator) ← all three merge here
                                    │
                              Phase 6 (API Controller)
                                    │
                              Phase 7 (Angular UI)
                                    │
                              Phase 8 (Polish)
```

---

## Milestone Checkpoints

| Milestone | What you prove | After phase |
|-----------|---------------|-------------|
| **M1** | Notion MCP server starts, tools listed, a query returns data | 2 |
| **M2** | Claude calls Notion tool, gets data, produces answer | 5 |
| **M3** | Full HTTP round-trip: POST message → get answer | 6 |
| **M4** | User chats in Angular, asks about Notion, gets answers | 7 |

**M2 is the riskiest milestone — it validates the entire agentic loop. Prioritize reaching it quickly.**

---

## Trade-off Note

Without streaming (SignalR/SSE), the user sees nothing until the full response is ready. For complex questions (Claude makes 3-4 tool calls), this can mean 10-30 seconds of waiting with just a spinner. This is fine for an MVP. If the wait becomes a UX problem later, consider adding:

- **SSE (Server-Sent Events)** — simpler, native browser support, one-line Angular `EventSource`
- **SignalR** — if bidirectional communication is needed later
