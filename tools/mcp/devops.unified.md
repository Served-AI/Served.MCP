---
type: mcp-tool
name: DevOps
version: 2026.10.1
domain: devops
tags: [mcp, devops, git, forge, github, gitlab, azure-devops, ci-cd, pipelines, pull-requests, runners, registry]
description: Git repository management, pull requests, and CI/CD pipeline operations.
---

# DevOps MCP Tools

Connect Git repositories (GitHub, GitLab, Azure DevOps), track pull requests, and monitor CI/CD pipelines.

---

## Quick Start

```
1. GetUserContext() → Get workspace
2. DevOpGetRepositories() → List connected repos
3. DevOpGetPullRequestInbox() → Pull requests waiting for your review
4. DevOpGetPipelineRuns() → Newest CI runs
5. DevOpGetRunners() → Runner status
```

---

## Repositories

### DevOpGetRepositories

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

### DevOpGetRepository

Get details for a specific repository.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |

---

### DevOpCreateRepository

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
DevOpCreateRepository(request: {
  provider: "GitHub",
  repositoryName: "company/frontend",
  repositoryUrl: "https://github.com/company/frontend",
  accessToken: "ghp_xxx...",
  setupWebhook: true
})
```

#### Example - GitLab

```
DevOpCreateRepository(request: {
  provider: "GitLab",
  repositoryName: "namespace/project",
  repositoryUrl: "https://gitlab.com/namespace/project",
  accessToken: "glpat-xxx..."
})
```

#### Example - Azure DevOps

```
DevOpCreateRepository(request: {
  provider: "AzureDevOps",
  repositoryName: "my-repo",
  repositoryUrl: "https://dev.azure.com/org/project/_git/my-repo",
  accessToken: "xxx...",
  azureOrganization: "my-org",
  azureProject: "my-project"
})
```

---

### DevOpUpdateRepository

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

### DevOpDeleteRepository

Remove a repository connection.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| repositoryId | int | Yes | - | Repository ID |

---

## Pull Requests

Pull requests come from Forge and from the GitHub repositories linked to the workspace, in one shape. A pull request
is named by `source` (forge | github), `owner`, `repo` and `number` — every list row carries them.
These tools call `api/devops/pull-requests`, the same routes as the webapp's pull request pages.

### DevOpGetPullRequestInbox

The caller's inbox across the workspace's repositories.

#### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| scope | string | No | review | review (waiting for their review), mine (opened by them), all |
| state | string | No | open | open, closed, merged, all |
| repositoryId | int | No | - | Only this workspace repository |
| limit | int | No | 25 | Per source and page |
| page | int | No | 1 | Each source's page; `hasMore` says whether there is a next |

#### Response

```json
{
  "sources": [
    { "source": "forge", "owner": "", "repo": "", "error": null },
    { "source": "github", "owner": "Served-AI", "repo": "Served.SDK", "error": "GitHub App has no pull_requests permission" }
  ],
  "items": [
    {
      "source": "forge", "owner": "served", "repo": "app", "number": 12,
      "title": "Runner drawer", "state": "open", "isDraft": false,
      "author": { "name": "Atlas", "userId": 2, "key": "2" },
      "sourceBranch": "feat/runners", "targetBranch": "master",
      "checksState": "success", "reviewDecision": "review_required",
      "labels": [{ "name": "devops", "color": "#0e8a16" }],
      "requestedReviewers": [{ "name": "Thomas", "userId": 1, "key": "1" }],
      "createdAt": "2026-10-11T09:00:00Z", "updatedAt": "2026-10-11T10:30:00Z"
    }
  ],
  "hasMore": false
}
```

A source that cannot be read carries its `error`; the others are still listed.

---

### DevOpGetPullRequests

Everyone's pull requests across the workspace's repositories (the inbox with scope `all`).

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| state | string | No | open | open, closed, merged, all |
| limit | int | No | 50 | Per source and page |
| repositoryId | int | No | - | Only this workspace repository |
| page | int | No | 1 | Page |

### DevOpGetPullRequestsByRepository

One workspace repository's pull requests: its Forge repository and the GitHub repository linked to it.
Parameters: `repositoryId` (required), `state`, `limit`, `page`.

---

### DevOpGetPullRequest

One pull request with commits, reviews, comments, line threads, checks and mergeability.

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| source | string | Yes | - | forge or github |
| owner | string | Yes | - | Repository owner |
| repo | string | Yes | - | Repository name |
| number | int | Yes | - | Pull request number |
| parts | string | No | all | commits,reviews,comments,checks,merge — `none` = the pull request alone |

### DevOpUpdatePullRequest

Change title, body, draft, labels, assignees or requested reviewers. Only what is given changes; lists replace the
current set. Labels by name, people by `key` (Forge: the Served user id, GitHub: the login) — see
DevOpGetPullRequestOptions.

```
DevOpUpdatePullRequest(source: "forge", owner: "served", repo: "app", number: 12,
  request: { reviewers: ["1"], labels: ["devops"], isDraft: false })
```

### DevOpClosePullRequest / DevOpReopenPullRequest

Close without merging, or reopen. Parameters: `source`, `owner`, `repo`, `number`. Both answer with the pull request.

### DevOpGetPullRequestOptions

The labels and people a pull request in the repository can be given. Parameters: `source`, `owner`, `repo`.

---

### Links to tasks and agent sessions

| Tool | Parameters | Description |
|------|------------|-------------|
| DevOpGetPullRequestsByTask | taskId | Pull requests linked to a task |
| DevOpGetPullRequestsBySession | sessionId | Pull requests an agent session created |
| DevOpGetPullRequestLink | source, owner, repo, number | The task and agent session it is linked to |
| DevOpLinkPullRequestToTask | source, owner, repo, number, taskId | Link a pull request to a task |
| DevOpUnlinkPullRequestFromTask | source, owner, repo, number | Remove the link |
| DevOpLinkPullRequestToSession | source, owner, repo, number, sessionId | Link it to the agent session that made it |
| DevOpGetPullRequestPipelineRuns | pullRequestId | CI runs recorded for a linked pull request |
| DevOpGetLatestPipelineRun | pullRequestId | The newest of them |

PRs auto-link via title/description: `Fixes SERVED-123`, `Closes #SERVED-456`, `Implements SERVED-789`.

---

## Pipelines (Forge workflow runs)

These tools read the tenant's Forge (`api/devops/forge-go/...`).

### DevOpGetPipelineRuns

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| owner | string | No | - | With repo: one repository's runs |
| repo | string | No | - | With owner: one repository's runs |
| branch | string | No | - | Only this branch |
| status | string | No | - | queued, in_progress, waiting, completed |
| limit | int | No | 20 | Max 100 |
| page | int | No | 1 | One repository only |

Without owner/repo: the newest runs across all the tenant's repositories.

#### Response

```json
{
  "total_count": 20,
  "workflow_runs": [
    {
      "id": 5512, "repo_full_name": "served/app", "workflow_name": "Auto Deploy", "run_number": 1874,
      "event": "push", "status": "completed", "conclusion": "success",
      "head_branch": "master", "head_sha": "39bd65da80…", "message": "fix(devops): …",
      "started_at": "2026-10-11T15:02:11Z", "completed_at": "2026-10-11T15:09:40Z"
    }
  ]
}
```

| Status | Conclusion (when completed) |
|--------|-----------------------------|
| queued, in_progress, waiting, completed | success, failure, cancelled, skipped |

### DevOpGetPipelineRun / DevOpGetPipelineRunJobs

A run with its jobs and steps (status, conclusion, runner, timings). Parameters: `owner`, `repo`, `runId`.

### DevOpGetPipelineRunLogs

A run's stored log lines, every job and step in order. Parameters: `owner`, `repo`, `runId`, `tail` (default 200, 0 = all).

### DevOpGetPipelineStepLogs

One step's lines. Parameters: `owner`, `repo`, `runId`, `jobId`, `stepId` (from DevOpGetPipelineRun), `fromLine`.

### DevOpCancelPipelineRun / DevOpRerunPipelineRun

Cancel a queued or running run; run it again (`failedJobsOnly: true` re-runs only the failed jobs in place).
Both need the ManagePipelines permission.

---

## Runners

### DevOpGetRunners

The tenant's runners.

```json
{
  "total_count": 3,
  "runners": [
    { "id": 7, "name": "eden-runner-1", "os": "linux", "arch": "amd64", "version": "2026.10.6",
      "status": "online", "busy": false, "labels": ["self-hosted", "linux", "has:kubernetes"],
      "last_seen_at": "2026-10-11T16:20:05Z" }
  ]
}
```

| Tool | Parameters | Description |
|------|------------|-------------|
| DevOpGetRunner | runnerId | One runner |
| DevOpDeleteRunner | runnerId | Remove it from Forge (stop the process on its host first) |

---

## Registry and mirrors

| Tool | Parameters | Description |
|------|------------|-------------|
| DevOpGetRegistryUpstreams | - | Pull-through upstreams of the platform registry (system administrators) |
| DevOpGetRegistryReplications | - | Push-replication rules with their last outcome (system administrators) |
| DevOpRunRegistryReplication | name, image, tag | Push one image through a rule now |
| DevOpDeleteRegistryUpstream / DevOpDeleteRegistryReplication | name | Remove one |
| DevOpGetMirrors / DevOpGetMirror | - / owner, repo | Pull-mirrors and their sync status |
| DevOpSyncMirror | owner, repo | Sync now |
| DevOpDeleteMirror | owner, repo | Stop mirroring (the repository stays) |

Creating or changing upstreams, replication rules and mirrors carries credentials and is not an MCP tool — use the
webapp (DevOps → Registry, Repositories).

---

## Workflows

### Review queue

```
1. DevOpGetPullRequestInbox(scope: "review") → What waits for me
2. DevOpGetPullRequest(source, owner, repo, number, parts: "checks,merge") → CI and mergeability
3. DevOpUpdatePullRequest(..., request: { reviewers: [...] }) or DevOpClosePullRequest(...)
```

### Debug a failed pipeline

```
1. DevOpGetPipelineRuns(owner: "served", repo: "app", status: "completed") → Find the failed run
2. DevOpGetPipelineRun(owner, repo, runId) → The failed job and step
3. DevOpGetPipelineStepLogs(owner, repo, runId, jobId, stepId) → The error
4. DevOpRerunPipelineRun(owner, repo, runId, failedJobsOnly: true)
```

### Runner health

```
1. DevOpGetRunners() → status per runner (online / offline / busy) and last_seen_at
```

### Link agent work to a task

```
1. DevOpGetPullRequestInbox(scope: "mine") → The PR the agent opened
2. DevOpLinkPullRequestToSession(source, owner, repo, number, sessionId)
3. DevOpLinkPullRequestToTask(source, owner, repo, number, taskId)
```

---

## Webhooks

| Provider | Events |
|----------|--------|
| GitHub | `pull_request`, `workflow_run`, `push` |
| GitLab | `merge_request`, `pipeline`, `push` |
| Azure DevOps | `git.pullrequest.*`, `build.complete` |

Webhook URL: `https://app.served.dk/api/devops/webhooks/{provider}`

---

## Errors

| Error | Cause | Solution |
|-------|-------|----------|
| RepositoryAlreadyConnected | Repo exists | Use existing or disconnect first |
| InvalidAccessToken | Token expired/invalid | Update with new token |
| WebhookSetupFailed | Token missing scopes | Check required scopes |
| RepositoryNotFound | Invalid ID | Use DevOpGetRepositories |
| RateLimited | Too many API calls | Wait and retry |

---

## Hints

- Always set up webhooks for automatic sync
- Link PRs to tasks for traceability
- Check CI before merge with DevOpGetPullRequest(parts: "checks,merge")
- Rotate tokens regularly with DevOpUpdateRepository

---

## CLI Equivalent Commands

| MCP Tool | CLI Command |
|----------|-------------|
| DevOpGetRepositories | `served devops repos` |
| DevOpGetPullRequests | `served devops prs` |
| DevOpGetPipelineRuns | `served forge runs` |
| DevOpGetPipelineRun | `served forge jobs <run-id>` |
| DevOpGetPipelineStepLogs | `served forge debug <run-id>` |
| DevOpGetRunners | `served forge runners` |
| DevOpCancelPipelineRun | `served forge cancel <run-id>` |

---

*Last Updated: 2026-10-11*
