using System.CommandLine;
using PaperlessDotNet.Cli.Configuration;
using PaperlessDotNet.Cli.Output;
using PaperlessDotNet.Cli.Services;
using VMelnalksnis.PaperlessDotNet.Documents;

namespace PaperlessDotNet.Cli.Commands;

public static class DocumentsCommand
{
    private static Option<string?> UrlOption { get; } = new("--url")
    {
        Description = "Paperless-ngx base URL (uses stored default if omitted)"
    };
    private static Option<OutputFormat> OutputOption { get; } = new("--output")
    {
        Description = "Output format: table or json",
        DefaultValueFactory = _ => OutputFormat.Table
    };

    public static Command Create(ICredentialService credentialService, PaperlessClientFactory clientFactory)
    {
        var documentsCommand = new Command("documents", "Manage documents")
        {
            UrlOption,
            CreateListCommand(clientFactory),
            CreateGetCommand(clientFactory),
            CreateDownloadCommand(clientFactory),
            CreateCreateCommand(clientFactory),
            CreateUpdateCommand(clientFactory),
            CreateDeleteCommand(clientFactory),
            CreateMetadataCommand(clientFactory),
            CreatePreviewCommand(clientFactory),
            CreateThumbnailCommand(clientFactory),
            CreateCustomFieldsCommand(clientFactory)
        };

        return documentsCommand;
    }

    private static Command CreateListCommand(PaperlessClientFactory clientFactory)
    {
        var pageSizeOption = new Option<int>("--page-size")
        {
            Description = "Number of documents per page",
            DefaultValueFactory = _ => 25
        };

        var command = new Command("list", "List all documents")
        {
            UrlOption,
            OutputOption,
            pageSizeOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var format = parseResult.GetValue(OutputOption);
            var pageSize = parseResult.GetValue(pageSizeOption);

            var documents = new List<Document>();
            await foreach (var doc in client.Documents.GetAll(pageSize, cancellationToken))
                documents.Add(doc);

            WriteListOutput(documents, format, d => new { d.Id, d.Title, d.OriginalFileName, Added = d.Added.ToString() });
            return 0;
        });

        return command;
    }

    private static Command CreateGetCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };

        var command = new Command("get", "Get document details")
        {
            UrlOption,
            OutputOption,
            idArgument
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var format = parseResult.GetValue(OutputOption);

            var doc = await client.Documents.Get(id, cancellationToken);
            if (doc is null)
            {
                Console.Error.WriteLine($"Document {id} not found.");
                return 1;
            }

            WriteOutput(doc, format);
            return 0;
        });

        return command;
    }

    private static Command CreateDownloadCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };
        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output file path (default: document filename)"
        };
        var originalOption = new Option<bool>("--original")
        {
            Description = "Download original file instead of archived"
        };

        var command = new Command("download", "Download a document")
        {
            UrlOption,
            idArgument,
            outputOption,
            originalOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var outputPath = parseResult.GetValue(outputOption);
            var original = parseResult.GetValue(originalOption);

            using var content = original
                ? await client.Documents.DownloadOriginal(id, cancellationToken)
                : await client.Documents.Download(id, cancellationToken);

            var fileName = content.ContentDisposition?.FileName ?? $"document-{id}";
            var path = outputPath?.FullName ?? fileName.Trim('"');

            await using (var fileStream = File.Create(path))
            await using (var contentStream = content.Content)
                await contentStream.CopyToAsync(fileStream, cancellationToken);

            Console.WriteLine($"Downloaded to {path}");
            return 0;
        });

        return command;
    }

    private static Command CreateCreateCommand(PaperlessClientFactory clientFactory)
    {
        var fileArgument = new Argument<FileInfo>("file") { Description = "File to upload" };
        var titleOption = new Option<string?>("--title");
        var correspondentOption = new Option<int?>("--correspondent-id");
        var documentTypeOption = new Option<int?>("--document-type-id");
        var tagIdsOption = new Option<int[]?>("--tag-ids")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var command = new Command("create", "Upload a new document")
        {
            UrlOption,
            fileArgument,
            titleOption,
            correspondentOption,
            documentTypeOption,
            tagIdsOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var file = parseResult.GetValue(fileArgument)!;
            if (!file.Exists)
            {
                Console.Error.WriteLine($"File not found: {file.FullName}");
                return 1;
            }

            await using var stream = file.OpenRead();
            var creation = new DocumentCreation(stream, file.Name)
            {
                Title = parseResult.GetValue(titleOption),
                CorrespondentId = parseResult.GetValue(correspondentOption),
                DocumentTypeId = parseResult.GetValue(documentTypeOption),
                TagIds = parseResult.GetValue(tagIdsOption)
            };

            var result = await client.Documents.Create(creation);

            if (result is DocumentCreated created)
            {
                Console.WriteLine($"Document created with ID: {created.Id}");
                return 0;
            }
            if (result is ImportFailed failed)
            {
                Console.Error.WriteLine($"Import failed: {failed.Result}");
                return 1;
            }
            if (result is ImportStarted)
            {
                Console.WriteLine("Import started (document ID may not be available in older Paperless versions).");
                return 0;
            }

            return 1;
        });

        return command;
    }

    private static Command CreateUpdateCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };
        var titleOption = new Option<string?>("--title");
        var correspondentOption = new Option<int?>("--correspondent-id");
        var documentTypeOption = new Option<int?>("--document-type-id");
        var tagIdsOption = new Option<int[]?>("--tag-ids")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var command = new Command("update", "Update document metadata")
        {
            UrlOption,
            OutputOption,
            idArgument,
            titleOption,
            correspondentOption,
            documentTypeOption,
            tagIdsOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var update = new DocumentUpdate
            {
                Title = parseResult.GetValue(titleOption),
                CorrespondentId = parseResult.GetValue(correspondentOption),
                DocumentTypeId = parseResult.GetValue(documentTypeOption),
                TagIds = parseResult.GetValue(tagIdsOption)
            };

            var doc = await client.Documents.Update(id, update);
            Console.WriteLine($"Document {id} updated.");
            WriteOutput(doc!, parseResult.GetValue(OutputOption));
            return 0;
        });

        return command;
    }

    private static Command CreateDeleteCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };

        var command = new Command("delete", "Delete a document")
        {
            UrlOption,
            idArgument
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            await client.Documents.Delete(id);
            Console.WriteLine($"Document {id} deleted.");
            return 0;
        });

        return command;
    }

    private static Command CreateMetadataCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };

        var command = new Command("metadata", "Get document metadata")
        {
            UrlOption,
            OutputOption,
            idArgument
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var format = parseResult.GetValue(OutputOption);
            var metadata = await client.Documents.GetMetadata(id, cancellationToken);
            WriteOutput(metadata, format);
            return 0;
        });

        return command;
    }

    private static Command CreatePreviewCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };
        var originalOption = new Option<bool>("--original");

        var command = new Command("preview", "Preview document (outputs to stdout)")
        {
            UrlOption,
            idArgument,
            originalOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var original = parseResult.GetValue(originalOption);

            var content = original
                ? await client.Documents.DownloadOriginalPreview(id, cancellationToken)
                : await client.Documents.DownloadPreview(id, cancellationToken);

            await using var stdout = Console.OpenStandardOutput();
            await using var contentStream = content.Content;
            await contentStream.CopyToAsync(stdout, cancellationToken);
            return 0;
        });

        return command;
    }

    private static Command CreateThumbnailCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Document ID" };
        var outputOption = new Option<FileInfo?>("--output")
        {
            Description = "Output file path (default: document-{id}-thumb.png)"
        };

        var command = new Command("thumbnail", "Download document thumbnail")
        {
            UrlOption,
            idArgument,
            outputOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var outputPath = parseResult.GetValue(outputOption);
            var content = await client.Documents.DownloadThumbnail(id, cancellationToken);

            var path = outputPath?.FullName ?? $"document-{id}-thumb.png";
            await using (var fileStream = File.Create(path))
            await using (var contentStream = content.Content)
                await contentStream.CopyToAsync(fileStream, cancellationToken);

            Console.WriteLine($"Thumbnail saved to {path}");
            return 0;
        });

        return command;
    }

    private static Command CreateCustomFieldsCommand(PaperlessClientFactory clientFactory)
    {
        var listCommand = new Command("list", "List custom fields")
        {
            UrlOption,
            OutputOption
        };

        listCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var format = parseResult.GetValue(OutputOption);
            var fields = new List<object>();
            await foreach (var field in client.Documents.GetCustomFields(cancellationToken))
                fields.Add(field);
            WriteOutput(fields, format);
            return 0;
        });

        var command = new Command("custom-fields", "Manage custom fields")
        {
            listCommand
        };

        return command;
    }

    private static VMelnalksnis.PaperlessDotNet.IPaperlessClient? GetClient(ParseResult parseResult, PaperlessClientFactory factory)
    {
        try
        {
            var urlStr = parseResult.GetValue(UrlOption);
            Uri? url = null;
            if (!string.IsNullOrEmpty(urlStr) && Uri.TryCreate(urlStr, UriKind.Absolute, out var parsed) && parsed.IsAbsoluteUri)
                url = parsed;

            return factory.CreateClient(url);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
    }

    private static void WriteOutput<T>(T value, OutputFormat format) where T : notnull
    {
        Console.WriteLine(OutputFormatters.ToJson(value));
    }

    private static void WriteListOutput<T>(IEnumerable<T> items, OutputFormat format, Func<T, object>? selector = null)
    {
        var toOutput = selector != null ? items.Select(selector) : items.Cast<object>();
        var list = toOutput.ToList();
        if (format == OutputFormat.Json)
            Console.WriteLine(OutputFormatters.ToJson(list));
        else
            foreach (var item in list)
                Console.WriteLine(OutputFormatters.ToJson(item));
    }

    private static void WriteOutput(IEnumerable<object> items, OutputFormat format)
    {
        var list = items.ToList();
        if (format == OutputFormat.Json)
            Console.WriteLine(OutputFormatters.ToJson(list));
        else
            foreach (var item in list)
                Console.WriteLine(OutputFormatters.ToJson(item));
    }
}
