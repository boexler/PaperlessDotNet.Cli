using System.Net.Http;
using System.CommandLine;
using System.Text.Json;
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

    public static Command Create(
        PaperlessClientFactory clientFactory,
        ITagCorrespondentUpdateService updateService,
        ICorrespondentMergeService mergeService,
        ICorrespondentMatchFixService matchFixService)
    {
        var command = new Command("correspondents", "Manage correspondents")
        {
            UrlOption,
            CreateListCommand(clientFactory),
            CreateGetCommand(clientFactory),
            CreateInspectCommand(clientFactory),
            CreateCreateCommand(clientFactory),
            CreateUpdateCommand(updateService),
            CreateMergeCommand(mergeService),
            CreateFixMatchCommand(matchFixService),
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

    private static Command CreateInspectCommand(PaperlessClientFactory clientFactory)
    {
        var idArgument = new Argument<int>("id") { Description = "Correspondent ID" };
        var fromListOption = new Option<bool>("--from-list")
        {
            Description = "Fetch from list API (as fix-match does) to compare with single GET"
        };

        var inspectCommand = new Command("inspect", "Fetch correspondent raw API response (id, name, match, matching_algorithm)")
        {
            UrlOption,
            idArgument,
            fromListOption
        };

        inspectCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var baseUrl = GetBaseUrl(parseResult);
            using var client = clientFactory.CreateHttpClient(baseUrl);
            var id = parseResult.GetValue(idArgument);
            var fromList = parseResult.GetValue(fromListOption);

            if (fromList)
            {
                var url = "api/correspondents/?page_size=100";
                while (url != null)
                {
                    using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
                    {
                        if (item.GetProperty("id").GetInt32() == id)
                        {
                            var listMatch = item.TryGetProperty("match", out var lm) ? lm.GetString() : null;
                            var listAlgo = item.TryGetProperty("matching_algorithm", out var lma) ? lma.GetInt32() : -1;
                            Console.WriteLine($"[FROM LIST] Id: {id}, Match: {listMatch ?? "(empty)"}, MatchingAlgorithm: {listAlgo} (4=Regex)");
                            return 0;
                        }
                    }
                    var nextVal = doc.RootElement.TryGetProperty("next", out var n) ? n.GetString() : null;
                    url = nextVal != null && Uri.TryCreate(nextVal, UriKind.Absolute, out var nextUri)
                        ? nextUri.PathAndQuery.TrimStart('/')
                        : nextVal;
                }
                Console.Error.WriteLine($"Correspondent {id} not found in list.");
                return 1;
            }

            using var singleResponse = await client.GetAsync($"api/correspondents/{id}/", cancellationToken).ConfigureAwait(false);
            if (!singleResponse.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Correspondent {id} not found: {singleResponse.StatusCode}");
                return 1;
            }

            var singleJson = await singleResponse.Content.ReadAsStringAsync(cancellationToken);
            using var singleDoc = JsonDocument.Parse(singleJson);
            var root = singleDoc.RootElement;
            var matchVal = root.TryGetProperty("match", out var m) ? m.GetString() : null;
            var matchingAlgorithmVal = root.TryGetProperty("matching_algorithm", out var ma) ? ma.GetInt32() : (int?)null;

            Console.WriteLine($"Id: {root.GetProperty("id").GetInt32()}");
            Console.WriteLine($"Name: {root.GetProperty("name").GetString()}");
            Console.WriteLine($"Match: {matchVal ?? "(empty)"}");
            Console.WriteLine($"MatchingAlgorithm: {matchingAlgorithmVal} (4=Regex)");
            return 0;
        });

        return inspectCommand;
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

    private static Command CreateFixMatchCommand(ICorrespondentMatchFixService matchFixService)
    {
        var idArgument = new Argument<int?>("id")
        {
            Description = "Correspondent ID to process (omit to process all)",
            Arity = ArgumentArity.ZeroOrOne
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Only output suggestions, do not apply updates"
        };
        var requireRegexOption = new Option<bool>("--require-regex")
        {
            Description = "Fix all with MatchingAlgorithm != 4 (regex). Without: only fix empty Match"
        };

        var fixMatchCommand = new Command("fix-match", "Fix correspondent Match by extracting URL domains from documents and setting regex")
        {
            UrlOption,
            idArgument,
            dryRunOption,
            requireRegexOption
        };

        fixMatchCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var dryRun = parseResult.GetValue(dryRunOption);
                var requireRegex = parseResult.GetValue(requireRegexOption);
                var correspondentId = parseResult.GetValue(idArgument);
                var baseUrl = GetBaseUrl(parseResult);

                var results = await matchFixService.FixMatchAsync(dryRun, requireRegex, baseUrl, correspondentId, cancellationToken);

                foreach (var r in results)
                {
                    var statusStr = r.Status switch
                    {
                        CorrespondentMatchFixStatus.Applied => "Updated",
                        CorrespondentMatchFixStatus.DryRun => "Would set",
                        CorrespondentMatchFixStatus.Skipped => "Skipped",
                        _ => r.Status.ToString()
                    };
                    Console.WriteLine($"Correspondent {r.CorrespondentId} \"{r.CorrespondentName}\" ({statusStr}): {r.Message}");
                }

                var applied = results.Count(r => r.Status == CorrespondentMatchFixStatus.Applied);
                var wouldSet = results.Count(r => r.Status == CorrespondentMatchFixStatus.DryRun);
                var skipped = results.Count(r => r.Status == CorrespondentMatchFixStatus.Skipped);
                Console.WriteLine();
                if (results.Count == 0)
                    Console.WriteLine("No correspondents need fixing (all have regex match, or no empty match without --require-regex).");
                else
                    Console.WriteLine($"Summary: {results.Count} candidate(s) | {(dryRun ? "Would set" : "Updated")}: {applied + wouldSet} | Skipped: {skipped}");

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

        return fixMatchCommand;
    }

    private static Command CreateMergeCommand(ICorrespondentMergeService mergeService)
    {
        var sourceArgument = new Argument<int>("source-id") { Description = "Correspondent ID to merge away (will be deleted)" };
        var targetArgument = new Argument<int>("target-id") { Description = "Correspondent ID to keep (documents will be reassigned here)" };

        var mergeCommand = new Command("merge", "Merge source correspondent into target (reassign documents, then delete source)")
        {
            UrlOption,
            sourceArgument,
            targetArgument
        };

        mergeCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var sourceId = parseResult.GetValue(sourceArgument);
                var targetId = parseResult.GetValue(targetArgument);
                var baseUrl = GetBaseUrl(parseResult);

                var count = await mergeService.MergeAsync(sourceId, targetId, baseUrl, cancellationToken);
                Console.WriteLine($"Merged correspondent {sourceId} into {targetId}. {count} document(s) reassigned.");
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

        return mergeCommand;
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
