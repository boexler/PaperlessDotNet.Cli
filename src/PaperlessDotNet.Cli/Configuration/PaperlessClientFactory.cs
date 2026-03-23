using System.Net.Http;
using System.Net.Mime;
using NodaTime;
using PaperlessDotNet.Cli.Services;
using VMelnalksnis.PaperlessDotNet;
using VMelnalksnis.PaperlessDotNet.Correspondents;
using VMelnalksnis.PaperlessDotNet.Documents;
using VMelnalksnis.PaperlessDotNet.DocumentTypes;
using VMelnalksnis.PaperlessDotNet.Serialization;
using VMelnalksnis.PaperlessDotNet.Tags;
using VMelnalksnis.PaperlessDotNet.Tasks;

namespace PaperlessDotNet.Cli.Configuration;

/// <summary>
/// Factory for creating configured Paperless API client instances.
/// </summary>
public sealed class PaperlessClientFactory
{
    private readonly ICredentialService _credentialService;

    public PaperlessClientFactory(ICredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    /// <summary>
    /// Creates an <see cref="IPaperlessClient"/> using stored credentials.
    /// </summary>
    /// <param name="baseUrl">Optional base URL. If null, uses the default stored credential.</param>
    /// <returns>The configured Paperless client.</returns>
    /// <exception cref="InvalidOperationException">When no credentials are stored.</exception>
    public IPaperlessClient CreateClient(Uri? baseUrl = null)
    {
        var credential = _credentialService.GetCredential(baseUrl);

        if (credential is null)
        {
            throw new InvalidOperationException(
                "No credentials found. Run 'paperless login --url <base-url> --token <api-token>' first.");
        }

        var (baseAddress, token) = credential.Value;

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + "/")
        };

        httpClient.DefaultRequestHeaders.Add("Accept", $"{MediaTypeNames.Application.Json}; version=2");
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);

        var serializerOptions = new PaperlessJsonSerializerOptions(DateTimeZoneProviders.Tzdb);
        var taskClient = new TaskClient(httpClient, serializerOptions);
        var correspondentClient = new CorrespondentClient(httpClient, serializerOptions);
        var documentClient = new DocumentClient(
            httpClient,
            serializerOptions,
            taskClient,
            TimeSpan.FromMilliseconds(250));
        var documentTypeClient = new DocumentTypeClient(httpClient, serializerOptions);
        var tagClient = new TagClient(httpClient, serializerOptions);

        return new PaperlessClient(
            correspondentClient,
            documentClient,
            documentTypeClient,
            tagClient);
    }
}
