# Served MCP Server

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue)](https://modelcontextprotocol.io)

MCP (Model Context Protocol) Server for AI assistants to interact with the Served platform via [UnifiedHQ](https://unifiedhq.ai). Enables Claude, GPT, and other AI models to access workspaces, projects, tasks, customers, and more.

## Links

| Resource | URL |
|----------|-----|
| **UnifiedHQ Platform** | [unifiedhq.ai](https://unifiedhq.ai) |
| **Forge DevOps** | [forge.unifiedhq.ai](https://forge.unifiedhq.ai) |
| **API Documentation** | [unifiedhq.ai/docs/api](https://unifiedhq.ai/docs/api) |
| **Served.SDK (NuGet)** | [nuget.org/packages/Served.SDK](https://www.nuget.org/packages/Served.SDK) |
| **MCP Protocol** | [modelcontextprotocol.io](https://modelcontextprotocol.io) |

## Features

- **40+ MCP Tools** - Full CRUD operations on Served entities
- **AI Intelligence** - Project health analysis, task decomposition, effort estimation
- **DevOps Integration** - Git repos, PRs, CI/CD pipelines
- **SDK Tracing** - OpenTelemetry observability via Served.SDK
- **Analytics** - View tool usage metrics at [forge.unifiedhq.ai/analytics](https://forge.unifiedhq.ai/analytics)
- **Fork & Extend** - Open source - customize for your needs

---

## Quick Start

### 1. Install and configure Claude Desktop

```bash
dotnet tool install -g Served.MCP --add-source https://git.unifiedhq.ai/nuget/served/v3/index.json
```

Add to your Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "served": {
      "command": "served-mcp",
      "env": {
        "SERVED_API_KEY": "your-api-key",
        "SERVED_TENANT": "your-workspace-slug"
      }
    }
  }
}
```

### 2. Get Your API Token

1. Go to [unifiedhq.ai/app/settings/api-keys](https://unifiedhq.ai/app/settings/api-keys)
2. Create a new API key with desired scopes
3. Copy the token to your config

### 3. Start Using

In Claude, you can now:
- "Show me my projects"
- "Create a task for Project X"
- "Log 2 hours on the Website task"
- "What's the health of Project Y?"

---

## Installation Options

### Option 1: .NET tool (recommended)

Needs the .NET 10 runtime. Every push to `main` publishes a new version to the Forge NuGet feed
(`.forge/workflows/publish.yaml`).

```bash
dotnet tool install -g Served.MCP --add-source https://git.unifiedhq.ai/nuget/served/v3/index.json
dotnet tool update  -g Served.MCP --add-source https://git.unifiedhq.ai/nuget/served/v3/index.json
served-mcp            # stdio MCP server
```

### Option 2: Clone & Run

```bash
# Clone the repo
git clone https://github.com/Served-AI/Served.MCP.git
cd served-mcp

# Run directly
dotnet run

# Or build and run
dotnet build -c Release
./bin/Release/net10.0/Served.MCP
```

### Option 3: From Source (ServedApp)

If you have the full ServedApp repository:

```bash
cd ServedApp/Served.MCP
dotnet run
```

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `SERVED_API_URL` | API base URL | `https://app.served.dk` |
| `SERVED_API_KEY` | API key (long-lived; wins over the token) | - |
| `SERVED_API_TOKEN` | JWT token | - |
| `SERVED_EMAIL` / `SERVED_PASSWORD` | Auto-login when there is no key and no valid token | - |
| `SERVED_TENANT` | Workspace slug | - |
| `SERVED_TRACING_ENABLED` | SDK tracing | `true` |
| `SERVED_ENVIRONMENT` | Environment name on traces | `development` |

Without `SERVED_API_KEY` and `SERVED_API_TOKEN` the server reads `~/.served/mcp/credentials.json`
(`{ "apiKey": "...", "token": "..." }`).

### Tracing

Tracing is on by default; turn it off with:

```bash
export SERVED_TRACING_ENABLED=false
```

View your traces at [forge.unifiedhq.ai/analytics](https://forge.unifiedhq.ai/analytics)

---

## MCP Tools Overview

### Context Tools

| Tool | Description |
|------|-------------|
| `GetUserContext` | Get user profile and workspaces. **Call this first.** |
| `GetTenantContext` | Get detailed tenant info (settings, categories). |
| `GetProjectContext` | Get project with tasks, team and recent activity. |

### Project Management

| Tool | Description |
|------|-------------|
| `GetProjects` | List all projects for workspace |
| `GetProjectDetails` | Get detailed project information |
| `CreateProject` | Create new project |
| `UpdateProject` | Update existing project |
| `DeleteProject` | Delete project |

### Task Management

| Tool | Description |
|------|-------------|
| `GetTasks` | Get tasks for project |
| `GetTaskDetails` | Get detailed task information |
| `CreateTask` | Create new task |
| `UpdateTask` | Update task |
| `DeleteTask` | Delete task |
| `CreateTasksBulk` | Bulk create tasks |

### Time Tracking

| Tool | Description |
|------|-------------|
| `SuggestTimeEntries` | AI suggestions for time registration |
| `AnalyzeTimePatterns` | Analyze user's time patterns |

### AI Intelligence

| Tool | Description |
|------|-------------|
| `AnalyzeProjectHealth` | Health check with score, risks, recommendations |
| `SuggestTaskDecomposition` | Suggestions for task breakdown |
| `EstimateEffort` | AI estimate based on history |
| `FindSimilarProjects` | Find similar projects |

### DevOps

| Tool | Description |
|------|-------------|
| `GetDevOpsRepositories` | List connected Git repos |
| `GetPullRequests` | Get PRs for workspace or repository |
| `GetPipelineRuns` | Get pipeline runs |
| `GetJobLog` | Get log output from a job |

See [tools/mcp/](tools/mcp/) for full documentation of all 40+ tools.

---

## Fork & Extend

This MCP server is open source. Fork it to:

1. **Add Custom Tools**: Extend with your own MCP tools
2. **Custom Integrations**: Connect to additional services
3. **Run Your Own Instance**: Host on your infrastructure

```bash
# Fork on GitHub, then:
git clone https://github.com/YOUR_USERNAME/served-mcp.git
cd served-mcp

# Add your custom tools in tools/
# Build and test
dotnet build
dotnet test

# Run your customized version
dotnet run
```

---

## Observability

### What Gets Traced

Every tool call captures:

| Attribute | Description |
|-----------|-------------|
| `mcp.tool.name` | Tool name (e.g., `GetTasks`) |
| `mcp.tool.success` | Whether the call succeeded |
| `mcp.session.id` | Session identifier |
| `mcp.agent.id` | Agent identifier |
| Duration | Execution time in ms |

### View Analytics

- **Forge Dashboard**: [forge.unifiedhq.ai/analytics](https://forge.unifiedhq.ai/analytics)
- **Your Workspace**: [unifiedhq.ai/app/analytics](https://unifiedhq.ai/app/analytics)

---

## API Environments

| Environment | URL |
|-------------|-----|
| **Production** | `https://app.served.dk` |
| **MCP Server** | `https://app.served.dk/mcp` |
| **Local Dev** | `http://localhost:5010` |

## Authentication

**MCP Tools:**
OAuth with scopes: `projects`, `tasks`, `customers`, `calendar`, `timetracking`, `employees`, `intelligence`, `customfields`, `devops`

**REST API:**
```
Authorization: Bearer <JWT_TOKEN>
```

---

## Changelog

### v2026.1.3 (2026-01-29)

- **UnifiedHQ Integration** - All endpoints now use `apis.unifiedhq.ai`
- **Forge Analytics** - Built-in tracing with dashboard support
- **Fork Support** - GitHub Actions workflow for forks
- **Documentation** - Complete overhaul with UnifiedHQ links

### v2026.1.2 (2026-01-17)

- **Unified File Format** - All documentation converted to `.unified.md` format
- **SDK Tracing** - Integrated with Served.SDK tracing infrastructure
- **OpenTelemetry** - Tool calls now emit spans and metrics
- **DevOps Enhancement** - Extended DevOps tools

### v2026.1.1

- Initial MCP server implementation
- 40+ tools for Served platform access

---

## Support

- **Documentation**: [unifiedhq.ai/docs](https://unifiedhq.ai/docs)
- **Issues**: [GitHub Issues](https://github.com/Served-AI/Served.MCP/issues)
- **Discord**: [UnifiedHQ Community](https://discord.gg/unifiedhq)

## License

MIT License - see [LICENSE](LICENSE) for details.

---

Built with love by [UnifiedHQ](https://unifiedhq.ai)
