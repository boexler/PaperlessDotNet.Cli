# PaperlessDotNet.Cli

A command-line interface for [Paperless-ngx](https://github.com/paperless-ngx/paperless-ngx) built on [PaperlessDotNet](https://github.com/VMelnalksnis/PaperlessDotNet).

## Features

- **Login/Logout**: Secure credential storage via Windows Credential Manager
- **Documents**: List, get, download, create, update, delete, metadata, preview, thumbnail
- **Tags**: Manage tags (list, get, create, update, delete)
- **Correspondents**: Manage correspondents (list, get, inspect, create, update, merge, fix-match, delete)
- **Document Types**: Manage document types
- **Custom Fields**: List custom fields

## Prerequisites

- Windows (for credential storage)
- .NET 8.0 Runtime (included in standalone build)

## Installation

### Option 1: Standalone Executable (Recommended)

Download the latest release and extract the `paperless.exe` to a folder in your PATH.

### Option 2: Build from Source

```bash
dotnet publish src/PaperlessDotNet.Cli/PaperlessDotNet.Cli.csproj -c Release -r win-x64
```

The executable will be in `src/PaperlessDotNet.Cli/bin/Release/net8.0/win-x64/publish/paperless.exe`.

## Quick Start

### 1. Login

Store your Paperless-ngx credentials:

```bash
paperless login --url https://paperless.example.com --token YOUR_API_TOKEN
```

Get your API token from Paperless-ngx: **Settings → Users → API Tokens**

### 2. List Documents

```bash
paperless documents list
```

### 3. Download a Document

```bash
paperless documents download 123 --output invoice.pdf
```

## Commands Reference

### Global Options

| Option | Description |
|--------|-------------|
| `--url <url>` | Paperless-ngx base URL (uses stored default from login if omitted) |
| `--output table` \| `json` | Output format for list/get commands (default: table) |
| `--page-size <n>` | Number of items per page for list commands (default: 25) |

### Login/Logout

| Command | Description |
|---------|-------------|
| `paperless login --url <url> --token <token>` (or `-u`, `-t`) | Store credentials in Windows Credential Manager |
| `paperless logout [--url <url>]` | Remove stored credentials (omit `--url` for default) |

### Documents

| Command | Description |
|---------|-------------|
| `paperless documents list` | List all documents |
| `paperless documents get <id>` | Get document details |
| `paperless documents download <id> [-o <path>] [--original]` | Download document (use `--original` for original file instead of archived) |
| `paperless documents create <file> [--title <title>] [--correspondent-id <id>] [--document-type-id <id>] [--tag-ids <id1> <id2> ...]` | Upload a new document |
| `paperless documents update <id> [--title <title>] [--correspondent-id <id>] [--document-type-id <id>] [--tag-ids <id1> <id2> ...]` | Update document metadata |
| `paperless documents delete <id>` | Delete a document |
| `paperless documents metadata <id>` | Get document metadata |
| `paperless documents preview <id> [--original]` | Output document preview to stdout |
| `paperless documents thumbnail <id> [-o <path>]` | Download document thumbnail |
| `paperless documents custom-fields list` | List all custom fields |

### Tags

| Command | Description |
|---------|-------------|
| `paperless tags list` | List all tags |
| `paperless tags get <id>` | Get tag details |
| `paperless tags create --name <name> [--color <int>]` | Create a tag |
| `paperless tags update <id> [--name <name>] [--color <hex>] [--match <pattern>] [--matching-algorithm <0-6>] [--is-insensitive] [--is-inbox-tag]` | Update a tag |
| `paperless tags delete <id>` | Delete a tag |

### Correspondents

| Command | Description |
|---------|-------------|
| `paperless correspondents list` | List all correspondents |
| `paperless correspondents get <id>` | Get correspondent details |
| `paperless correspondents inspect <id> [--from-list]` | Fetch raw API response (id, name, match, matching_algorithm) |
| `paperless correspondents create --name <name>` | Create a correspondent |
| `paperless correspondents update <id> [--name <name>] [--match <pattern>] [--matching-algorithm <0-6>] [--is-insensitive]` | Update a correspondent |
| `paperless correspondents fix-match [<id>] [--dry-run] [--require-regex] [--verbose]` | Fix Match by extracting domains from documents (URLs, emails, filenames) and setting regex. Without `--require-regex`: only fix empty Match. Use `--verbose` to see searched text when skipped. |
| `paperless correspondents merge <source-id> <target-id>` | Merge source into target (reassign documents, delete source) |
| `paperless correspondents delete <id>` | Delete a correspondent |

### Document Types

| Command | Description |
|---------|-------------|
| `paperless document-types list` | List all document types |
| `paperless document-types get <id>` | Get document type details |
| `paperless document-types create --name <name>` | Create a document type |
| `paperless document-types delete <id>` | Delete a document type |

### Custom Fields

| Command | Description |
|---------|-------------|
| `paperless documents custom-fields list` | List all custom fields |

## Output Formats

Use `--output json` or `--output table` (default) for list and get commands.

## Matching Algorithms

For `--matching-algorithm` in tags/correspondents: `0`=None, `1`=Any word, `2`=All words, `3`=Exact, `4`=Regex, `5`=Fuzzy, `6`=Automatic.

## Links

- [Paperless-ngx](https://github.com/paperless-ngx/paperless-ngx) - Document management system
- [PaperlessDotNet](https://github.com/VMelnalksnis/PaperlessDotNet) - .NET Paperless API client

## License

Apache 2.0
