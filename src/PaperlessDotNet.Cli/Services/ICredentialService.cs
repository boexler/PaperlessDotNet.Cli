namespace PaperlessDotNet.Cli.Services;

/// <summary>
/// Service for storing and retrieving Paperless-ngx credentials.
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Gets the credential for the specified base URL, or the default credential if URL is null.
    /// </summary>
    /// <param name="baseUrl">The Paperless-ngx base URL, or null for default.</param>
    /// <returns>The credential (URL as username, token as password), or null if not found.</returns>
    (Uri BaseAddress, string Token)? GetCredential(Uri? baseUrl = null);

    /// <summary>
    /// Stores the credential for the specified base URL.
    /// </summary>
    /// <param name="baseAddress">The Paperless-ngx base URL.</param>
    /// <param name="token">The API token.</param>
    void StoreCredential(Uri baseAddress, string token);

    /// <summary>
    /// Removes the stored credential for the specified base URL.
    /// </summary>
    /// <param name="baseUrl">The Paperless-ngx base URL, or null for default.</param>
    /// <returns>True if a credential was removed; false if none existed.</returns>
    bool RemoveCredential(Uri? baseUrl = null);
}
