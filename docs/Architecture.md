## Overview

AI chatbot that answers user questions based on data from their Notion workspace. Uses Claude as the LLM, MCP protocol to access Notion, and a simple request-response HTTP API.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| UI | Angular Web App |
| Backend | .NET (ASP.NET Core) |
| LLM | Claude API (Anthropic) |
| Notion Access | MCP Server for Notion (`@notionhq/notion-mcp-server`) |
| MCP Integration | .NET MCP SDK (`ModelContextProtocol` NuGet) |

---

## Key Design Decisions

| Decision | Choice |
|----------|--------|
| MCP server hosting | Run as a child process (stdio transport) managed by .NET backend |
| Tool discovery | Use `ListToolsAsync()` from the MCP SDK at startup; pass tool definitions to Claude's `tools` parameter automatically |
| Conversation memory | Store in-memory per session (simple) |
| Prompt design | System prompt instructs Claude to always use Notion tools for data questions and never fabricate Notion content |
| API style | Simple REST request-response (no SignalR, no streaming) |

---

## Not In Scope (for now)

- Authentication
- Caching layer
- Structured logging (Serilog)
- Persistent conversation history (DB)
- Rate limiting middleware
- SignalR / streaming

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Angular SPA                            │
│  ┌──────────┐  ┌───────────────────────────────────────┐    │
│  │ Chat UI  │  │ ChatService (HttpClient)              │    │
│  └──────────┘  └──────────────────┬────────────────────┘    │
└───────────────────────────────────┼─────────────────────────┘
                                    │ HTTPS (REST)
┌───────────────────────────────────┼─────────────────────────┐
│            .NET Backend (ASP.NET Core)                      │
│                                   ▼                         │
│  ┌─────────────────────────────────────────────────────┐    │
│  │               ChatController                        │    │
│  └──────────────────────┬──────────────────────────────┘    │
│                         │                                    │
│  ┌──────────────────────▼──────────────────────────────┐    │
│  │              AgentOrchestrator                       │    │
│  │                                                      │    │
│  │  1. Receives user message                           │    │
│  │  2. Calls Claude API with tools (MCP tool defs)     │    │
│  │  3. When Claude requests a tool → routes to MCP     │    │
│  │  4. Returns tool results to Claude                  │    │
│  │  5. Returns final answer to controller              │    │
│  └─────────┬────────────────────────┬──────────────────┘    │
│            │                        │                        │
│  ┌─────────▼──────────┐  ┌─────────▼──────────────────┐    │
│  │  ClaudeApiClient    │  │  McpToolRouter             │    │
│  │  (Anthropic API)    │  │  (.NET MCP SDK)            │    │
│  └─────────────────────┘  └─────────┬──────────────────┘    │
│                                      │ stdio                 │
│  ┌─────────────────────┐             │                       │
│  │  PromptBuilder      │             │                       │
│  ├─────────────────────┤             │                       │
│  │  ConversationManager│             │                       │
│  └─────────────────────┘             │                       │
└──────────────────────────────────────┼──────────────────────┘
                                       │
                    ┌──────────────────▼──────────────────┐
                    │     MCP Server for Notion            │
                    │  (@notionhq/notion-mcp-server)       │
                    │                                      │
                    │  Tools exposed:                      │
                    │   • search_pages                     │
                    │   • read_page                        │
                    │   • read_database                    │
                    │   • query_database                   │
                    └──────────────────┬──────────────────┘
                                       │ Notion API
                                       ▼
                              ┌─────────────────┐
                              │  Notion Workspace │
                              └─────────────────┘
```

---

## Agentic Tool-Use Loop

```
         ProcessMessageAsync("What's overdue?")
                    │
    ┌───────────────▼────────────────┐
    │  Send messages + tools to Claude│◄────────────────┐
    └───────────────┬────────────────┘                  │
                    │                                    │
              ┌─────▼─────┐                              │
              │ tool_use  │──Yes──► Execute via MCP      │
              │ in reply? │        Add result to history──┘
              └─────┬─────┘          (loop back)
                    │ No
                    ▼
             Return text to user
              (exit loop)

Max iterations: 10 (safety guard)
```

---