using System.CommandLine;
using PaperlessDotNet.Cli.Configuration;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var credentialService = new WindowsCredentialService();
        var clientFactory = new PaperlessClientFactory(credentialService);

        var rootCommand = new RootCommand("CLI for Paperless-ngx built on PaperlessDotNet")
        {
            Commands.LoginCommand.Create(credentialService),
            Commands.LogoutCommand.Create(credentialService),
            Commands.DocumentsCommand.Create(credentialService, clientFactory),
            Commands.TagsCommand.Create(clientFactory),
            Commands.CorrespondentsCommand.Create(clientFactory),
            Commands.DocumentTypesCommand.Create(clientFactory)
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
