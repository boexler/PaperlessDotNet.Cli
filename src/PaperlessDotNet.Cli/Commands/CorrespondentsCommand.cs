using System.Net.Http;
using System.CommandLine;
using PaperlessDotNet.Cli.ApiExtensions;
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

    public static Command Create(PaperlessClientFactory clientFactory, ITagCorrespondentUpdateService updateService)
    {
        var command = new Command("correspondents", "Manage correspondents")
        {
            UrlOption,
            CreateListCommand(clientFactory),
            CreateGetCommand(clientFactory),
            CreateCreateCommand(clientFactory),
            CreateUpdateCommand(updateService),
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

    private static Command CreateUpdateCommand(ITagCorrespondentUpdateService updateService)
    {
        var idArgument = new Argument<int>("id") { Description = "Correspondent ID" };
        var nameOption = new Option<string?>("--name");
        var matchOption = new Option<string?>("--match");
        var matchingAlgorithmOption = new Option<int?>("--matching-algorithm")
        {
            Description = "0=None, 1=Any word, 2=All words, 3=Exact, 4=Regex, 5=Fuzzy, 6=Automatic"
        };
        var isInsensitiveOption = new Option<bool?>("--is-insensitive");

        var updateCommand = new Command("update", "Update a correspondent")
        {
            UrlOption,
            OutputOption,
            idArgument,
            nameOption,
            matchOption,
            matchingAlgorithmOption,
            isInsensitiveOption
        };

        updateCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var id = parseResult.GetValue(idArgument);
                var baseUrl = GetBaseUrl(parseResult);

                var patch = new CorrespondentPatchDto
                {
                    Name = parseResult.GetValue(nameOption),
                    Match = parseResult.GetValue(matchOption),
                    MatchingAlgorithm = parseResult.GetValue(matchingAlgorithmOption),
                    IsInsensitive = parseResult.GetValue(isInsensitiveOption)
                };

                var hasAnyField = patch.Name is not null || patch.Match is not null
                    || patch.MatchingAlgorithm is not null || patch.IsInsensitive is not null;
                if (!hasAnyField)
                {
                    Console.Error.WriteLine("Specify at least one field to update: --name, --match, --matching-algorithm, --is-insensitive");
                    return 1;
                }

                var result = await updateService.UpdateCorrespondentAsync(id, patch, baseUrl, cancellationToken);
                Console.WriteLine($"Correspondent {id} updated.");
                Console.WriteLine(OutputFormatters.ToJson(result));
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        });

        return updateCommand;
    }

    private static Uri? GetBaseUrl(ParseResult parseResult)
    {
        var urlStr = parseResult.GetValue(UrlOption);
        if (string.IsNullOrEmpty(urlStr) || !Uri.TryCreate(urlStr, UriKind.Absolute, out var parsed) || !parsed.IsAbsoluteUri)
            return null;
        return parsed;
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
