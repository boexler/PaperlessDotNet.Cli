using System.CommandLine;
using PaperlessDotNet.Cli.Configuration;
using PaperlessDotNet.Cli.Output;
using PaperlessDotNet.Cli.Services;
using VMelnalksnis.PaperlessDotNet.Tags;

namespace PaperlessDotNet.Cli.Commands;

public static class TagsCommand
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
        var tagsCommand = new Command("tags", "Manage tags")
        {
            UrlOption,
            CreateListCommand(clientFactory),
            CreateGetCommand(clientFactory),
            CreateCreateCommand(clientFactory),
            CreateDeleteCommand(clientFactory)
        };

        return tagsCommand;
    }

    private static Command CreateListCommand(PaperlessClientFactory clientFactory)
    {
        var pageSizeOption = new Option<int>("--page-size") { DefaultValueFactory = _ => 25 };

        var command = new Command("list", "List all tags")
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
            var tags = new List<Tag>();
            await foreach (var tag in client.Tags.GetAll(pageSize, cancellationToken))
                tags.Add(tag);

            WriteListOutput(tags, format, t => new { t.Id, t.Name, t.Colour });
            return 0;
        });

        return command;
    }

    private static Command CreateGetCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Tag ID" };

        var command = new Command("get", "Get tag details")
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
            var tag = await client.Tags.Get(id, cancellationToken);
            if (tag is null)
            {
                Console.Error.WriteLine($"Tag {id} not found.");
                return 1;
            }

            Console.WriteLine(OutputFormatters.ToJson(tag));
            return 0;
        });

        return command;
    }

    private static Command CreateCreateCommand(PaperlessClientFactory clientFactory)
    {
        var nameOption = new Option<string>("--name") { Required = true };
        var colorOption = new Option<int?>("--color");

        var command = new Command("create", "Create a tag")
        {
            UrlOption,
            nameOption,
            colorOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var name = parseResult.GetValue(nameOption)!;
            var color = parseResult.GetValue(colorOption);
            var creation = new TagCreation(name) { Colour = color };
            var tag = await client.Tags.Create(creation);
            Console.WriteLine($"Tag created with ID: {tag.Id}");
            Console.WriteLine(OutputFormatters.ToJson(tag));
            return 0;
        });

        return command;
    }

    private static Command CreateDeleteCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Tag ID" };

        var command = new Command("delete", "Delete a tag")
        {
            UrlOption,
            idArgument
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var client = GetClient(parseResult, clientFactory);
            if (client is null) return 1;

            var id = parseResult.GetValue(idArgument);
            await client.Tags.Delete(id);
            Console.WriteLine($"Tag {id} deleted.");
            return 0;
        });

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
