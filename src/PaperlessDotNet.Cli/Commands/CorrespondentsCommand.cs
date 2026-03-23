using System.CommandLine;
using PaperlessDotNet.Cli.Configuration;
using PaperlessDotNet.Cli.Output;
using PaperlessDotNet.Cli.Services;
using VMelnalksnis.PaperlessDotNet.Correspondents;

namespace PaperlessDotNet.Cli.Commands;

public static class CorrespondentsCommand
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

    public static Command Create(PaperlessClientFactory clientFactory)
    {
        var command = new Command("correspondents", "Manage correspondents")
        {
            UrlOption,
            CreateListCommand(clientFactory),
            CreateGetCommand(clientFactory),
            CreateCreateCommand(clientFactory),
            CreateDeleteCommand(clientFactory)
        };

        return command;
    }

    private static Command CreateListCommand(PaperlessClientFactory clientFactory)
    {
        var pageSizeOption = new Option<int>("--page-size") { DefaultValueFactory = _ => 25 };

        var listCommand = new Command("list", "List all correspondents")
        {
            UrlOption,
            OutputOption,
            pageSizeOption
        };

        listCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var format = parseResult.GetValue(OutputOption);
            var pageSize = parseResult.GetValue(pageSizeOption);
            var items = new List<Correspondent>();
            await foreach (var item in client.Correspondents.GetAll(pageSize, cancellationToken))
                items.Add(item);

            WriteListOutput(items, format, c => new { c.Id, c.Name, c.Slug });
            return 0;
        });

        return listCommand;
    }

    private static Command CreateGetCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Correspondent ID" };

        var getCommand = new Command("get", "Get correspondent details")
        {
            UrlOption,
            OutputOption,
            idArgument
        };

        getCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            var format = parseResult.GetValue(OutputOption);
            var item = await client.Correspondents.Get(id, cancellationToken);
            if (item is null)
            {
                Console.Error.WriteLine($"Correspondent {id} not found.");
                return 1;
            }

            Console.WriteLine(OutputFormatters.ToJson(item));
            return 0;
        });

        return getCommand;
    }

    private static Command CreateCreateCommand(PaperlessClientFactory clientFactory)
    {
        var nameOption = new Option<string>("--name") { Required = true };

        var createCommand = new Command("create", "Create a correspondent")
        {
            UrlOption,
            nameOption
        };

        createCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var name = parseResult.GetValue(nameOption)!;
            var creation = new CorrespondentCreation(name);
            var item = await client.Correspondents.Create(creation);
            Console.WriteLine($"Correspondent created with ID: {item.Id}");
            Console.WriteLine(OutputFormatters.ToJson(item));
            return 0;
        });

        return createCommand;
    }

    private static Command CreateDeleteCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Correspondent ID" };

        var deleteCommand = new Command("delete", "Delete a correspondent")
        {
            UrlOption,
            idArgument
        };

        deleteCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            await client.Correspondents.Delete(id);
            Console.WriteLine($"Correspondent {id} deleted.");
            return 0;
        });

        return deleteCommand;
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
}
