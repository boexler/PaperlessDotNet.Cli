using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Merges correspondents via Paperless bulk_edit and correspondents API.
/// </summary>
public sealed class CorrespondentMergeService : ICorrespondentMergeService
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

    public CorrespondentMergeService(ICredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    /// <inheritdoc />
    public async Task<int> MergeAsync(int sourceId, int targetId, Uri? baseUrl, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
            throw new InvalidOperationException("Source and target correspondent must be different.");

        using var client = CreateHttpClient(baseUrl);

        var documentIds = await GetDocumentIdsByCorrespondentAsync(client, sourceId, cancellationToken).ConfigureAwait(false);
        if (documentIds.Count > 0)
        {
            await BulkSetCorrespondentAsync(client, documentIds, targetId, cancellationToken).ConfigureAwait(false);
        }

        await DeleteCorrespondentAsync(client, sourceId, cancellationToken).ConfigureAwait(false);
        return documentIds.Count;
    }

    private static async Task<List<int>> GetDocumentIdsByCorrespondentAsync(HttpClient client, int correspondentId, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var url = $"api/documents/?correspondent__id={correspondentId}&page_size=100";
        while (url != null)
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<DocumentsListResponse>(DeserializerOptions, cancellationToken).ConfigureAwait(false);
            if (json?.Results == null) break;

            foreach (var doc in json.Results)
                ids.Add(doc.Id);

            url = json.Next; // Next is full URL from API, GetAsync accepts it
        }

        return ids;
    }

    private static async Task BulkSetCorrespondentAsync(HttpClient client, List<int> documentIds, int correspondentId, CancellationToken cancellationToken)
    {
        var body = new
        {
            documents = documentIds,
            method = "set_correspondent",
            parameters = new { correspondent = correspondentId }
        };
        var requestBody = JsonSerializer.Serialize(body, SerializerOptions);
        using var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/documents/bulk_edit/") { Content = content };
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Bulk edit failed: {response.StatusCode} - {message}");
        }
    }

    private static async Task DeleteCorrespondentAsync(HttpClient client, int id, CancellationToken cancellationToken)
    {
        using var response = await client.DeleteAsync($"api/correspondents/{id}/", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Delete correspondent {id} failed: {response.StatusCode} - {message}");
        }
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

    private sealed class DocumentsListResponse
    {
        public List<DocumentListItem>? Results { get; set; }
        public string? Next { get; set; }
    }

    private sealed class DocumentListItem
    {
        public int Id { get; set; }
    }
}
