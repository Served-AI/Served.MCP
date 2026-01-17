---
type: mcp-tool
name: DevOps
version: 2026.1.2
domain: devops
tags: [mcp, devops, git, github, gitlab, azure-devops, ci-cd, pipelines, pull-requests]
description: Git repository management, pull requests, and CI/CD pipeline operations.
---

# DevOps MCP Tools

Connect Git repositories (GitHub, GitLab, Azure DevOps), track pull requests, and monitor CI/CD pipelines.

---

## Quick Start

```
1. GetUserContext() → Get workspace
2. GetDevOpsRepositories() → List connected repos
3. GetPullRequests() → See open PRs
4. GetLatestPipelineRun(pullRequestId) → Check CI status
```

---

## Repositories

### GetDevOpsRepositories

List all connected Git repositories.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| activeOnly | bool | No | true | Only active repos |

#### Response

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
      "webhookActive": true,
      "lastSyncedAt": "2026-01-17T10:30:00Z"
    }
  ],
  "hint": "Found 2 connected repositories."
}
```

---

### GetDevOpsRepository

Get details for a specific repository.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |

---

### ConnectRepository

Connect a new Git repository.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| provider | string | Yes | - | GitHub, GitLab, or AzureDevOps |
| repositoryName | string | Yes | - | Repo name (e.g., owner/repo) |
| repositoryUrl | string | Yes | - | Full URL to repository |
| accessToken | string | Yes | - | PAT for API access |
| defaultBranch | string | No | main | Default branch |
| description | string | No | null | Repository description |
| isPrivate | bool | No | false | Is private? |
| setupWebhook | bool | No | true | Setup webhook automatically |
| azureOrganization | string | No | - | Azure DevOps org (required for AzureDevOps) |
| azureProject | string | No | - | Azure DevOps project (required for AzureDevOps) |

#### Provider Token Scopes

| Provider | Required Scopes |
|----------|----------------|
| GitHub | `repo`, `admin:repo_hook` |
| GitLab | `api`, `read_repository` |
| Azure DevOps | `Code (Read & Write)`, `Service Hooks` |

#### Example - GitHub

```
ConnectRepository(
  provider: "GitHub",
  repositoryName: "company/frontend",
  repositoryUrl: "https://github.com/company/frontend",
  accessToken: "ghp_xxx...",
  setupWebhook: true
)
```

#### Example - GitLab

```
ConnectRepository(
  provider: "GitLab",
  repositoryName: "namespace/project",
  repositoryUrl: "https://gitlab.com/namespace/project",
  accessToken: "glpat-xxx..."
)
```

#### Example - Azure DevOps

```
ConnectRepository(
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

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |
| isActive | bool | No | - | Enable/disable |
| defaultBranch | string | No | - | New default branch |
| description | string | No | - | New description |
| accessToken | string | No | - | New access token |

---

### DisconnectRepository

Remove a repository connection.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |

---

## Pull Requests

### GetPullRequests

Get pull requests for workspace or specific repository.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | No | - | Filter by repository |
| state | string | No | - | Open, Merged, Closed |
| limit | int | No | 50 | Max count |

#### PR States

| State | Description |
|-------|-------------|
| Open | PR is open |
| Draft | PR is in draft mode |
| review_requested | Review requested |
| changes_requested | Changes requested |
| Approved | PR approved |
| Merged | PR merged |
| Closed | PR closed without merge |

#### Response

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
      "taskId": 456,
      "createdAt": "2026-01-15T09:00:00Z"
    }
  ]
}
```

---

### GetTaskPullRequests

Get pull requests linked to a specific task.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| taskId | int | Yes | - | Task ID |

#### Response Hint

```json
{
  "hint": "No PRs linked. Link via 'Fixes SERVED-123' in PR description."
}
```

---

### GetAgentSessionPullRequests

Get pull requests created by a CLI agent session.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| sessionId | int | Yes | - | Agent session ID |

---

### LinkPullRequestToTask

Link a pull request to a Served task.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| pullRequestId | int | Yes | - | Served PR ID (not external number) |
| taskId | int | Yes | - | Task ID |

#### Auto-Linking Patterns

PRs auto-link via title/description:
- `Fixes SERVED-123`
- `Closes #SERVED-456`
- `Implements SERVED-789`

---

## Pipeline/CI

### GetPipelineRuns

Get pipeline runs for a pull request or repository.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| pullRequestId | int | No | - | PR ID (one required) |
| repositoryId | int | No | - | Repository ID |
| limit | int | No | 50 | Max count |

#### Pipeline Status

| Status | Description |
|--------|-------------|
| Pending | Waiting to start |
| InProgress | Currently running |
| Completed | Finished |

#### Pipeline Conclusion

| Conclusion | Description |
|------------|-------------|
| Success | All OK |
| Failure | Failed |
| Cancelled | Cancelled |
| Skipped | Skipped |

---

### GetLatestPipelineRun

Get the latest CI status for a pull request.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| pullRequestId | int | Yes | - | PR ID |

#### Response

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
  "summary": "CI Status: ✅ OK (Success)",
  "hint": "Pipeline completed successfully! Ready to merge."
}
```

---

### GetPipelineJobs

Get jobs for a specific pipeline (GitLab).

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |
| pipelineId | long | Yes | - | Pipeline ID |

#### Response

```json
{
  "success": true,
  "pipelineId": 12345,
  "count": 5,
  "jobs": [
    {
      "id": 67890,
      "name": "build",
      "stage": "build",
      "status": "success",
      "duration": 120,
      "webUrl": "https://gitlab.com/..."
    }
  ],
  "summary": "✅ All jobs passed"
}
```

---

### GetJobLog

Get log output from a specific job.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |
| jobId | long | Yes | - | Job ID |
| tail | int | No | 100 | Last N lines |

---

### RetryJob

Retry a failed job.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |
| jobId | long | Yes | - | Job ID |

---

### CancelJob

Cancel a running job.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |
| jobId | long | Yes | - | Job ID |

---

## Workflows

### Setup Project with Git Integration

```
1. GetUserContext() → Find workspace
2. CreateProject(name: "Mobile App") → Create project
3. ConnectRepository(provider: "GitHub", ...) → Link repo
4. GetPullRequests() → See PRs
```

### Check CI Before Merge

```
1. GetPullRequests(state: "Open") → Find open PRs
2. GetLatestPipelineRun(pullRequestId) → Check CI
3. If "Success" → Ready to merge
```

### Debug Failed Pipeline

```
1. GetLatestPipelineRun(pullRequestId) → See status
2. GetPipelineJobs(repositoryId, pipelineId) → Find failed job
3. GetJobLog(repositoryId, jobId) → Read error logs
4. RetryJob(repositoryId, jobId) → Retry after fix
```

### Link Agent Work to Task

```
1. GetAgentSessionPullRequests(sessionId) → Find agent PRs
2. LinkPullRequestToTask(pullRequestId, taskId) → Link to task
```

---

## Webhooks

| Provider | Events |
|----------|--------|
| GitHub | `pull_request`, `workflow_run`, `push` |
| GitLab | `merge_request`, `pipeline`, `push` |
| Azure DevOps | `git.pullrequest.*`, `build.complete` |

Webhook URL: `https://app.served.dk/api/devops/webhook/{provider}`

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| RepositoryAlreadyConnected | Repo exists | Use existing or disconnect first |
| InvalidAccessToken | Token expired/invalid | Update with new token |
| WebhookSetupFailed | Token missing scopes | Check required scopes |
| RepositoryNotFound | Invalid ID | Use GetDevOpsRepositories |
| RateLimited | Too many API calls | Wait and retry |

---

## Hints

- Always set up webhooks for automatic sync
- Link PRs to tasks for traceability
- Check CI before merge with GetLatestPipelineRun
- Rotate tokens regularly with UpdateRepository

---

## CLI Equivalent Commands

| MCP Tool | CLI Command |
|----------|-------------|
| GetDevOpsRepositories | `served devops repos` |
| GetPullRequests | `served devops prs` |
| GetLatestPipelineRun | `served gitlab status` |
| GetJobLog | `served gitlab logs <job-id>` |
| RetryJob | `served gitlab retry` |

---

*Last Updated: 2026-01-17*
