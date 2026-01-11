# DevOps MCP Tools

Connect Git repositories (GitHub, GitLab, Azure DevOps) to Served, track pull requests, and monitor CI/CD pipelines.

---

## Repositories

### GetDevOpsRepositories

List all connected Git repositories for a workspace.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `activeOnly` | bool | Nej | Kun aktive repos (default: true) |

**Eksempel:**

```json
{
  "success": true,
  "count": 2,
  "repositories": [
    {
      "id": 1,
      "provider": "GitHub",
      "repositoryName": "company/frontend",
      "repositoryUrl": "https://github.com/company/frontend",
      "defaultBranch": "main",
      "isActive": true,
      "webhookActive": true
    }
  ]
}
```

---

### GetDevOpsRepository

Get details for a specific repository.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `repositoryId` | int | Ja | Repository ID |

---

### ConnectRepository

Connect a new Git repository to the workspace.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `provider` | string | Ja | `GitHub`, `GitLab`, eller `AzureDevOps` |
| `repositoryName` | string | Ja | Repo navn (fx `owner/repo`) |
| `repositoryUrl` | string | Ja | Fuld URL til repository |
| `accessToken` | string | Ja | PAT til API adgang |
| `defaultBranch` | string | Nej | Default branch (default: `main`) |
| `description` | string | Nej | Repository beskrivelse |
| `isPrivate` | bool | Nej | Er privat? (default: false) |
| `setupWebhook` | bool | Nej | Opsæt webhook automatisk (default: true) |
| `azureOrganization` | string | Nej | Azure DevOps org (krævet for AzureDevOps) |
| `azureProject` | string | Nej | Azure DevOps projekt (krævet for AzureDevOps) |

**Provider-specifikke krav:**

| Provider | Token Scopes |
|----------|--------------|
| GitHub | `repo`, `admin:repo_hook` |
| GitLab | `api`, `read_repository` |
| Azure DevOps | `Code (Read & Write)`, `Service Hooks` |

**Eksempel (GitHub):**

```
ConnectRepository(
  tenantId: 1,
  provider: "GitHub",
  repositoryName: "company/frontend",
  repositoryUrl: "https://github.com/company/frontend",
  accessToken: "ghp_xxx...",
  setupWebhook: true
)
```

**Eksempel (GitLab):**

```
ConnectRepository(
  tenantId: 1,
  provider: "GitLab",
  repositoryName: "namespace/project",
  repositoryUrl: "https://gitlab.com/namespace/project",
  accessToken: "glpat-xxx..."
)
```

**Eksempel (Azure DevOps):**

```
ConnectRepository(
  tenantId: 1,
  provider: "AzureDevOps",
  repositoryName: "my-repo",
  repositoryUrl: "https://dev.azure.com/org/project/_git/my-repo",
  accessToken: "xxx...",
  azureOrganization: "my-org",
  azureProject: "my-project"
)
```

---

### UpdateRepository

Update repository settings.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `repositoryId` | int | Ja | Repository ID |
| `isActive` | bool | Nej | Aktiver/deaktiver |
| `defaultBranch` | string | Nej | Ny default branch |
| `description` | string | Nej | Ny beskrivelse |
| `accessToken` | string | Nej | Nyt access token |

---

### DisconnectRepository

Remove a repository connection.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `repositoryId` | int | Ja | Repository ID |

---

## Pull Requests

### GetPullRequests

Get pull requests for workspace or specific repository.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `repositoryId` | int | Nej | Filter by repository |
| `state` | string | Nej | `Open`, `Merged`, `Closed` |
| `limit` | int | Nej | Max antal (default: 50) |

**PR States:**

| State | Beskrivelse |
|-------|-------------|
| `Open` | PR er åben |
| `Draft` | PR er i draft mode |
| `review_requested` | Review anmodet |
| `changes_requested` | Ændringer anmodet |
| `Approved` | PR godkendt |
| `Merged` | PR merged |
| `Closed` | PR lukket uden merge |

**Eksempel response:**

```json
{
  "success": true,
  "count": 3,
  "pullRequests": [
    {
      "id": 42,
      "externalPrNumber": 123,
      "title": "Fix login bug",
      "state": "Open",
      "url": "https://github.com/company/frontend/pull/123",
      "sourceBranch": "fix/login-bug",
      "targetBranch": "main",
      "authorUsername": "developer",
      "ciStatus": "Success",
      "taskId": 456
    }
  ]
}
```

---

### GetTaskPullRequests

Get pull requests linked to a specific task.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `taskId` | int | Ja | Task ID |

---

### GetAgentSessionPullRequests

Get pull requests created by a CLI agent session.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `sessionId` | int | Ja | Agent session ID |

---

### LinkPullRequestToTask

Link a pull request to a Served task.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `pullRequestId` | int | Ja | Served PR ID (ikke eksternt nummer) |
| `taskId` | int | Ja | Task ID |

**Automatisk linking:**

PRs kan også auto-linkes via PR titel/beskrivelse:
- `Fixes SERVED-123`
- `Closes #SERVED-456`
- `Implements SERVED-789`

---

## Pipeline/CI

### GetPipelineRuns

Get pipeline runs for a pull request or repository.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `tenantId` | int | Ja | Workspace ID |
| `pullRequestId` | int | Nej | PR ID (en af de to er påkrævet) |
| `repositoryId` | int | Nej | Repository ID |
| `limit` | int | Nej | Max antal (default: 50) |

**Pipeline Status:**

| Status | Beskrivelse |
|--------|-------------|
| `Pending` | Venter på start |
| `InProgress` | Kører nu |
| `Completed` | Færdig |

**Pipeline Conclusion:**

| Conclusion | Beskrivelse |
|------------|-------------|
| `Success` | Alt OK |
| `Failure` | Fejlet |
| `Cancelled` | Annulleret |
| `Skipped` | Sprunget over |

---

### GetLatestPipelineRun

Get the latest CI status for a pull request.

**Parametre:**

| Navn | Type | Krævet | Beskrivelse |
|------|------|--------|-------------|
| `pullRequestId` | int | Ja | PR ID |

**Eksempel response:**

```json
{
  "success": true,
  "pullRequestId": 42,
  "latestRun": {
    "id": 99,
    "status": "Completed",
    "conclusion": "Success",
    "pipelineName": "CI Pipeline",
    "url": "https://github.com/company/frontend/actions/runs/123",
    "durationSeconds": 245
  },
  "summary": "CI Status: OK (Success)",
  "hint": "Pipeline completed successfully!"
}
```

---

## Workflows

### Opsæt nyt projekt med Git integration

```
1. GetUserContext() -> Find workspace
2. CreateProject(tenantId, "Mobile App") -> Opret projekt
3. ConnectRepository(tenantId, "GitHub", "company/mobile-app", ...) -> Link repo
4. GetPullRequests(tenantId) -> Se PRs
```

### Tjek CI status før merge

```
1. GetPullRequests(tenantId, state="Open") -> Find åbne PRs
2. GetLatestPipelineRun(pullRequestId) -> Tjek CI
3. Hvis "Success" -> Klar til merge
```

### Link agent arbejde til task

```
1. GetAgentSessionPullRequests(sessionId) -> Find agent PRs
2. LinkPullRequestToTask(prId, taskId) -> Link til task
```

---

## Webhooks

Served modtager automatisk webhook events når de er konfigureret:

| Provider | Events |
|----------|--------|
| GitHub | `pull_request`, `workflow_run`, `push` |
| GitLab | `merge_request`, `pipeline`, `push` |
| Azure DevOps | `git.pullrequest.*`, `build.complete` |

Webhook URL format:
```
https://app.served.dk/api/devops/webhook/{provider}
```

---

## Se også

- [Git Project Setup Skill](/.claude/skills/git-project-setup/SKILL.md)
- [DevOps Integration Docs](/docs/tasks/features/DEVOPS_INTEGRATION_WEBHOOKS.md)
