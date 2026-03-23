# PaperlessDotNet.Cli

A command-line interface for [Paperless-ngx](https://github.com/paperless-ngx/paperless-ngx) built on [PaperlessDotNet](https://github.com/VMelnalksnis/PaperlessDotNet).

## Features

- **Login/Logout**: Secure credential storage via Windows Credential Manager
- **Documents**: List, get, download, create, update, delete documents
- **Tags**: Manage tags (list, get, create, delete)
- **Correspondents**: Manage correspondents
- **Document Types**: Manage document types
- **Custom Fields**: List and create custom fields

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

### Login/Logout

| Command | Description |
|---------|-------------|
| `paperless login --url <url> --token <token>` | Store credentials in Windows Credential Manager |
| `paperless logout [--url <url>]` | Remove stored credentials |

### Documents

| Command | Description |
|---------|-------------|
| `paperless documents list` | List all documents |
| `paperless documents get <id>` | Get document details |
| `paperless documents download <id>` | Download document (archived or original) |
| `paperless documents create <file>` | Upload a new document |
| `paperless documents update <id>` | Update document metadata |
| `paperless documents delete <id>` | Delete a document |
| `paperless documents metadata <id>` | Get document metadata |
| `paperless documents preview <id>` | Preview document in browser |
| `paperless documents thumbnail <id>` | Download thumbnail |

### Tags

| Command | Description |
|---------|-------------|
| `paperless tags list` | List all tags |
| `paperless tags get <id>` | Get tag details |
| `paperless tags create --name <name>` | Create a tag |
| `paperless tags delete <id>` | Delete a tag |

### Correspondents

| Command | Description |
|---------|-------------|
| `paperless correspondents list` | List all correspondents |
| `paperless correspondents get <id>` | Get correspondent details |
| `paperless correspondents create --name <name>` | Create a correspondent |
| `paperless correspondents delete <id>` | Delete a correspondent |

### Document Types

| Command | Description |
|---------|-------------|
| `paperless document-types list` | List all document types |
| `paperless document-types get <id>` | Get document type details |
| `paperless document-types create --name <name>` | Create a document type |
| `paperless document-types delete <id>` | Delete a document type |

## Output Formats

Use `--output json` or `--output table` (default) for list and get commands.

## Links

- [Paperless-ngx](https://github.com/paperless-ngx/paperless-ngx) - Document management system
- [PaperlessDotNet](https://github.com/VMelnalksnis/PaperlessDotNet) - .NET Paperless API client

## License

Apache 2.0
