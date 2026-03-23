using System.Runtime.Versioning;
using Meziantou.Framework.Win32;

namespace PaperlessDotNet.Cli.Services;

/// <summary>
/// Windows Credential Manager implementation of credential storage.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialService : ICredentialService
{
    private const string ApplicationName = "PaperlessDotNet.Cli";

    /// <inheritdoc />
    public (Uri BaseAddress, string Token)? GetCredential(Uri? baseUrl = null)
    {
        if (baseUrl is not null)
        {
            var targetName = GetTargetName(baseUrl);
            var credential = CredentialManager.ReadCredential(targetName);
            return credential is null ? null : ParseCredential(credential);
        }

        // When no URL specified, try default first, then enumerate for any stored credential
        var defaultCred = CredentialManager.ReadCredential(ApplicationName);
        if (defaultCred is not null)
        {
            var parsed = ParseCredential(defaultCred);
            if (parsed is not null)
                return parsed;
        }

        var all = CredentialManager.EnumerateCredentials($"{ApplicationName}*");
        foreach (var cred in all)
        {
            var parsed = ParseCredential(cred);
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    private static (Uri BaseAddress, string Token)? ParseCredential(Meziantou.Framework.Win32.Credential credential)
    {
        if (string.IsNullOrEmpty(credential.UserName) || string.IsNullOrEmpty(credential.Password))
            return null;

        if (!Uri.TryCreate(credential.UserName, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri)
            return null;

        return (uri, credential.Password);
    }

    /// <inheritdoc />
    public void StoreCredential(Uri baseAddress, string token)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentNullException.ThrowIfNull(token);

        var targetName = GetTargetName(baseAddress);
        var urlString = baseAddress.ToString().TrimEnd('/');

        CredentialManager.WriteCredential(
            targetName,
            urlString,
            token,
            comment: null,
            CredentialPersistence.LocalMachine);
    }

    /// <inheritdoc />
    public bool RemoveCredential(Uri? baseUrl = null)
    {
        var targetName = GetTargetName(baseUrl);
        var credential = CredentialManager.ReadCredential(targetName);

        if (credential is null)
            return false;

        try
        {
            CredentialManager.DeleteCredential(targetName);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string GetTargetName(Uri? baseUrl)
    {
        if (baseUrl is null)
            return ApplicationName;

        var host = baseUrl.Host;
        return string.IsNullOrEmpty(host) ? ApplicationName : $"{ApplicationName}:{host}";
    }
}
