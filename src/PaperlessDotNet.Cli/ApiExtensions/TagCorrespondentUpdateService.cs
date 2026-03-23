// WORKAROUND: Tag/Correspondent Update via direct HTTP PATCH.
// PaperlessDotNet does not yet expose Update. Remove this module and switch
// to client.Tags.Update / client.Correspondents.Update when available.

using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Temporary implementation: updates tags and correspondents via direct HTTP PATCH.
/// Remove when PaperlessDotNet adds Update support.
/// </summary>
public sealed class TagCorrespondentUpdateService : ITagCorrespondentUpdateService
{
    private readonly ICredentialService _credentialService;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TagCorrespondentUpdateService(ICredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    /// <inheritdoc />
    public async Task<TagPatchResponse> UpdateTagAsync(
        int id,
        TagPatchDto patch,
        Uri? baseUrl,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateHttpClient(baseUrl);

        using var content = JsonContent.Create(patch, options: SerializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/tags/{id}/") { Content = content };
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"PATCH /api/tags/{id}/ failed: {response.StatusCode} - {message}");
        }

        return (await response.Content.ReadFromJsonAsync<TagPatchResponse>(DeserializerOptions, cancellationToken)
            .ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task<CorrespondentPatchResponse> UpdateCorrespondentAsync(
        int id,
        CorrespondentPatchDto patch,
        Uri? baseUrl,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateHttpClient(baseUrl);

        using var content = JsonContent.Create(patch, options: SerializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/correspondents/{id}/") { Content = content };
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"PATCH /api/correspondents/{id}/ failed: {response.StatusCode} - {message}");
        }

        return (await response.Content.ReadFromJsonAsync<CorrespondentPatchResponse>(DeserializerOptions, cancellationToken)
            .ConfigureAwait(false))!;
    }

    private HttpClient CreateHttpClient(Uri? baseUrl)
    {
        var credential = _credentialService.GetCredential(baseUrl)
            ?? throw new InvalidOperationException(
                "No credentials found. Run 'paperless login --url <base-url> --token <api-token>' first.");

        var (baseAddress, token) = credential;
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + "/")
        };

        httpClient.DefaultRequestHeaders.Add("Accept", $"{MediaTypeNames.Application.Json}; version=2");
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);

        return httpClient;
    }
}
