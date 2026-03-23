using System.CommandLine;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli.Commands;

public static class LogoutCommand
{
    public static Command Create(ICredentialService credentialService)
    {
        var urlOption = new Option<string?>("--url", "-u")
        {
            Description = "The Paperless-ngx base URL to remove (omit for default)"
        };

        var command = new Command("logout", "Remove stored credentials from Windows Credential Manager")
        {
            urlOption
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var urlStr = parseResult.GetValue(urlOption);
            Uri? url = null;
            if (!string.IsNullOrEmpty(urlStr) && Uri.TryCreate(urlStr, UriKind.Absolute, out var parsed) && parsed.IsAbsoluteUri)
                url = parsed;

            var removed = credentialService.RemoveCredential(url);
            if (removed)
                Console.WriteLine("Credentials removed.");
            else
                Console.WriteLine("No credentials found to remove.");
            return Task.FromResult(0);
        });

        return command;
    }
}
