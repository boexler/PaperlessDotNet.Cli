using System.CommandLine;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli.Commands;

public static class LoginCommand
{
    public static Command Create(ICredentialService credentialService)
    {
        var urlOption = new Option<string>("--url", "-u")
        {
            Description = "The Paperless-ngx base URL (e.g. https://paperless.example.com)",
            Required = true
        };

        var tokenOption = new Option<string>("--token", "-t")
        {
            Description = "The API token",
            Required = true
        };

        var command = new Command("login", "Store credentials in Windows Credential Manager")
        {
            urlOption,
            tokenOption
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var urlStr = parseResult.GetValue(urlOption)!;
            var token = parseResult.GetValue(tokenOption)!;

            if (!Uri.TryCreate(urlStr, UriKind.Absolute, out var url) || !url.IsAbsoluteUri)
            {
                Console.Error.WriteLine("Invalid URL. Use full URL like https://paperless.example.com");
                return Task.FromResult(1);
            }

            credentialService.StoreCredential(url, token);
            Console.WriteLine($"Credentials stored for {url}");
            return Task.FromResult(0);
        });

        return command;
    }
}
