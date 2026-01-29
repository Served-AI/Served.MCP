---
type: mcp-tool
name: Files
version: 2026.1.2
domain: files
tags: [mcp, files, filesystem, search, security, tooling-auth]
description: Local filesystem operations with tooling authorization protection. Search, analyze, and manage files.
---

# Files MCP Tools

Local filesystem operations with tooling-auth protection.

---

## served_file_find

Find files with smart filtering and tooling-auth protection.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | No | home | Starting directory |
| name | string | No | - | Glob pattern for filename |
| regex | string | No | - | Regex pattern for filename |
| ext | string | No | - | Extensions (comma-separated) |
| preset | string | No | - | File type preset |
| type | string | No | f | f (files), d (dirs), all |
| depth | int | No | 0 | Max depth (0 = unlimited) |
| size | string | No | - | Size filter: +100M, -1G |
| newer | string | No | - | Modified after: 7d, 24h, 2026-01-01 |
| older | string | No | - | Modified before |
| contains | string | No | - | File must contain text |
| allow | string | No | - | Regex to allow sensitive paths |
| max | int | No | 500 | Max results |
| hidden | bool | No | false | Include hidden files |
| sort | string | No | date | Sort by: date, size, name, ext |
| reverse | bool | No | false | Reverse sort order |

### File Type Presets

| Preset | Extensions |
|--------|------------|
| `code` | cs, ts, js, py, go, rs, java, kt, swift, rb, php, c, cpp |
| `web` | html, htm, css, scss, sass, less, vue, jsx, tsx, svelte |
| `config` | json, yaml, yml, toml, ini, conf, env, xml, plist |
| `docs` | md, txt, rst, doc, docx, pdf, rtf, odt |
| `media` | jpg, jpeg, png, gif, bmp, svg, mp3, wav, mp4, mov |
| `data` | csv, tsv, json, xml, sqlite, db, parquet, xlsx |
| `archive` | zip, tar, gz, bz2, 7z, rar, xz |

### Response

```json
{
  "success": true,
  "searchPath": "/Users/thomas/Work/Project",
  "count": 42,
  "scanned": 15000,
  "sensitiveSkipped": 3,
  "excludedSkipped": 120,
  "results": [
    {
      "path": "/Users/thomas/Work/Project/src/app.ts",
      "name": "app.ts",
      "size": 4521,
      "sizeHuman": "4.42 KB",
      "modified": "2026-01-16T10:30:00",
      "extension": "ts",
      "type": "file"
    }
  ],
  "hint": "Use 'allow' parameter to access 3 sensitive paths"
}
```

### Examples

```
// Find TypeScript files modified in last 7 days
served_file_find(path: "~/Work/Project", ext: "ts,tsx", newer: "7d")

// Find large video files
served_file_find(path: "~/Downloads", preset: "media", size: "+100M")

// Search code files containing "TODO"
served_file_find(path: "~/Projects", preset: "code", contains: "TODO")
```

---

## served_file_stats

Get directory statistics including file count, size breakdown, and top extensions.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | No | cwd | Directory to analyze |

### Response

```json
{
  "success": true,
  "path": "/Users/thomas/Work/Project",
  "fileCount": 2451,
  "directoryCount": 312,
  "totalSize": 1073741824,
  "totalSizeHuman": "1 GB",
  "topExtensionsBySize": [
    { "extension": "ts", "count": 450, "totalSize": 524288000, "totalSizeHuman": "500 MB" },
    { "extension": "json", "count": 120, "totalSize": 104857600, "totalSizeHuman": "100 MB" }
  ]
}
```

---

## served_file_duplicates

Find duplicate files by content hash (SHA256).

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | No | cwd | Directory to search |
| minSize | long | No | 1024 | Minimum file size (bytes) |
| max | int | No | 100 | Max duplicate groups |

### Response

```json
{
  "success": true,
  "searchPath": "/Users/thomas/Downloads",
  "duplicateGroupsFound": 15,
  "totalWastedSpace": 524288000,
  "totalWastedSpaceHuman": "500 MB",
  "duplicates": [
    {
      "hash": "A1B2C3D4E5F67890",
      "size": 104857600,
      "sizeHuman": "100 MB",
      "count": 3,
      "wastedSpace": 209715200,
      "wastedSpaceHuman": "200 MB",
      "files": [
        "/Users/thomas/Downloads/video.mp4",
        "/Users/thomas/Downloads/video (1).mp4",
        "/Users/thomas/Downloads/backup/video.mp4"
      ]
    }
  ]
}
```

---

## served_file_tree

Display directory structure as a tree.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| path | string | No | cwd | Root directory |
| depth | int | No | 3 | Max depth |
| hidden | bool | No | false | Include hidden files |
| files | bool | No | true | Include files (not just dirs) |

### Response

```json
{
  "success": true,
  "path": "/Users/thomas/Work/Project/src",
  "depth": 2,
  "tree": "src/\n├── components/\n│   ├── Button.tsx\n│   └── Modal.tsx\n├── utils/\n│   └── helpers.ts\n└── app.ts\n"
}
```

---

## served_file_auth_status

Check current tooling authorization configuration.

### Parameters

None.

### Response

```json
{
  "success": true,
  "enabled": true,
  "requireConfirmation": true,
  "auditLogging": true,
  "configPath": "/Users/thomas/.served/tooling-auth.json",
  "allowedPaths": [
    {
      "Pattern": "Library/Application Support",
      "Reason": "Activity data import",
      "AllowedAt": "2026-01-16T10:00:00Z",
      "ExpiresAt": "2026-01-17T10:00:00Z",
      "AllowedBy": "thomas"
    }
  ]
}
```

---

## served_file_auth_allow

Grant temporary access to a sensitive path pattern.

### Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| pattern | string | Yes | - | Regex pattern to allow |
| reason | string | No | MCP tool access | Reason for access |
| days | int | No | 1 | Days until expiration (0 = never) |

### Response

```json
{
  "success": true,
  "message": "Pattern 'Library/Application Support' allowed for 7 day(s)",
  "pattern": "Library/Application Support",
  "reason": "Import activity tracking data",
  "expiresAt": "2026-01-23 10:30:00"
}
```

---

## Sensitive Path Protection

The file tools automatically skip sensitive paths unless explicitly allowed.

### Built-in Sensitive Patterns

| Category | Patterns |
|----------|----------|
| SSH/Keys | `.ssh`, `id_rsa`, `id_ed25519`, `.pem`, `.key` |
| Cloud Credentials | `.aws`, `.azure`, `.gnupg`, `.gcloud` |
| Browser Data | `Google/Chrome`, `Mozilla/Firefox`, `Safari/LocalStorage` |
| Secrets | `credentials`, `secrets`, `.env` |
| System | `Keychains`, `private/var/db` |

### Excluded Paths (Performance)

| Category | Patterns |
|----------|----------|
| Node | `node_modules`, `.npm` |
| .NET | `bin/Debug`, `bin/Release`, `obj/`, `.nuget` |
| Python | `__pycache__`, `.venv`, `venv` |
| Build | `dist`, `build`, `.next` |
| Git | `.git/objects` |
| Apple | `DerivedData`, `xcuserdata` |

---

## Workflows

### Find and Analyze Project Files

```
1. served_file_stats({ path: "~/Work/Project" }) -> Get size breakdown
2. served_file_find({ path: "~/Work/Project", preset: "code" }) -> List code files
3. served_file_duplicates({ path: "~/Work/Project" }) -> Find wasted space
```

### Access Sensitive Data with Authorization

```
1. served_file_auth_status() -> Check current permissions
2. served_file_auth_allow({ pattern: "Library/.*", reason: "Data import", days: 1 })
3. served_file_find({ path: "~/Library", ext: "sqlite", allow: "Library/.*" })
```

### Clean Up Downloads

```
1. served_file_duplicates({ path: "~/Downloads", minSize: 1048576 }) -> Find large duplicates
2. served_file_find({ path: "~/Downloads", size: "+1G" }) -> Find huge files
3. served_file_find({ path: "~/Downloads", older: "30d" }) -> Find old files
```

---

## Hints

- Use presets for common file types
- Sensitive paths require explicit allow pattern
- Authorization expires automatically for safety
- Performance exclusions skip build artifacts

---

*Last Updated: 2026-01-17*
