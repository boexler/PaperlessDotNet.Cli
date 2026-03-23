using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PaperlessDotNet.Cli.Services;

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Fixes correspondent Match fields by extracting URL domains from associated documents
/// and setting them as regex patterns for auto-assignment.
/// </summary>
public sealed class CorrespondentMatchFixService : ICorrespondentMatchFixService
{
    private readonly ICredentialService _credentialService;
    private readonly ITagCorrespondentUpdateService _updateService;

    /// <summary>MatchingAlgorithm value for regex (Paperless-ngx).</summary>
    private const int RegexMatchingAlgorithm = 4;

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Extract domain from URLs: https?://(host) or bare domain.</summary>
    private static readonly Regex UrlDomainRegex = new(
        @"(?:https?://(?:www\.)?([^/\s?#]+)|(?<=[\s(])([a-zA-Z0-9][-a-zA-Z0-9]*\.[a-zA-Z]{2,})(?=[\s)/?#]|$))",
        RegexOptions.Compiled);

    /// <summary>Extract domain from email addresses, e.g. info@airless-experts.de -> airless-experts.de.</summary>
    private static readonly Regex EmailDomainRegex = new(
        @"[a-zA-Z0-9._%+-]+@([a-zA-Z0-9][-a-zA-Z0-9]*\.[a-zA-Z]{2,})",
        RegexOptions.Compiled);

    /// <summary>Extract domain from correspondent name, e.g. "GOG.com Team" -> "gog.com".</summary>
    private static readonly Regex NameDomainRegex = new(
        @"[a-zA-Z0-9][-a-zA-Z0-9]*\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    /// <summary>File extensions to exclude from domain extraction (rechnung.pdf, 30598.eml etc. are filenames, not URLs).</summary>
    private static readonly HashSet<string> FileExtensionBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "jpg", "jpeg", "png", "gif",
        "html", "htm", "txt", "csv", "xml", "json", "zip", "rar", "exe", "dll", "eml", "msg"
    };

    private enum DomainSource { FromBare, FromEmail, FromUrl }

    public CorrespondentMatchFixService(
        ICredentialService credentialService,
        ITagCorrespondentUpdateService updateService)
    {
        _credentialService = credentialService;
        _updateService = updateService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CorrespondentMatchFixResult>> FixMatchAsync(
        bool dryRun,
        bool requireRegex,
        Uri? baseUrl,
        int? correspondentId = null,
        IProgress<CorrespondentMatchFixResult>? progress = null,
        Action<int, string, string>? onSkippedNoDomains = null,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateHttpClient(baseUrl);
        var results = new List<CorrespondentMatchFixResult>();

        var correspondents = await GetAllCorrespondentsAsync(client, cancellationToken).ConfigureAwait(false);
        if (correspondentId is { } id)
            correspondents = correspondents.Where(c => c.Id == id).ToList();

        var candidates = correspondents
            .Where(c => IsCandidate(c, requireRegex))
            .ToList();

        foreach (var correspondent in candidates)
        {
            var result = await ProcessCorrespondentAsync(
                client, correspondent, dryRun, baseUrl, onSkippedNoDomains, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            progress?.Report(result);
        }

        return results;
    }

    private static bool IsCandidate(CorrespondentListItem c, bool requireRegex)
    {
        if (c.MatchingAlgorithm == RegexMatchingAlgorithm)
            return false; // Never overwrite when already Regex
        if (requireRegex)
            return true;
        return string.IsNullOrWhiteSpace(c.Match);
    }

    private static string? ExtractDomainFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var m = NameDomainRegex.Match(name);
        if (!m.Success) return null;
        var domain = ToRootDomain(m.Value);
        return IsFileExtension(domain) ? null : domain;
    }

    private async Task<CorrespondentMatchFixResult> ProcessCorrespondentAsync(
        HttpClient client,
        CorrespondentListItem correspondent,
        bool dryRun,
        Uri? baseUrl,
        Action<int, string, string>? onSkippedNoDomains,
        CancellationToken cancellationToken)
    {
        var domainFromName = ExtractDomainFromName(correspondent.Name);
        string? rootDomain;

        if (domainFromName is not null)
        {
            rootDomain = domainFromName;
        }
        else
        {
            var documentIds = await GetDocumentIdsByCorrespondentAsync(client, correspondent.Id, cancellationToken)
                .ConfigureAwait(false);

            if (documentIds.Count == 0)
            {
                return new CorrespondentMatchFixResult(
                    correspondent.Id,
                    correspondent.Name,
                    null,
                    CorrespondentMatchFixStatus.Skipped,
                    "No documents found - skipped");
            }

            var domains = new Dictionary<string, (int Count, DomainSource Source)>(StringComparer.OrdinalIgnoreCase);
            var searchText = new System.Text.StringBuilder();

            foreach (var docId in documentIds)
            {
                var (title, content, fileName) = await GetDocumentContentAsync(client, docId, cancellationToken).ConfigureAwait(false);
                var text = title + " " + content + " " + fileName;
                ExtractDomains(text, domains);
                searchText.Append(text).Append(' ');
            }

            if (domains.Count == 0)
            {
                var preview = searchText.ToString();
                if (preview.Length > 1500)
                    preview = preview[..1500] + "...";
                onSkippedNoDomains?.Invoke(correspondent.Id, correspondent.Name, preview);
                return new CorrespondentMatchFixResult(
                    correspondent.Id,
                    correspondent.Name,
                    null,
                    CorrespondentMatchFixStatus.Skipped,
                    "No URL domain found in documents - skipped");
            }

            rootDomain = SelectBestDomain(domains);
        }

        var matchRegex = ".*" + Regex.Escape(rootDomain) + ".*";

        if (dryRun)
        {
            return new CorrespondentMatchFixResult(
                correspondent.Id,
                correspondent.Name,
                matchRegex,
                CorrespondentMatchFixStatus.DryRun,
                $"Would set: {matchRegex}");
        }

        var patch = new CorrespondentPatchDto
        {
            Match = matchRegex,
            MatchingAlgorithm = RegexMatchingAlgorithm,
            IsInsensitive = true
        };

        await _updateService.UpdateCorrespondentAsync(correspondent.Id, patch, baseUrl, cancellationToken)
            .ConfigureAwait(false);

        return new CorrespondentMatchFixResult(
            correspondent.Id,
            correspondent.Name,
            matchRegex,
            CorrespondentMatchFixStatus.Applied,
            $"Set: {matchRegex}");
    }

    /// <summary>Extract root domain only (no subdomains). e.g. bilder.baur.de -> baur.de.</summary>
    private static string ToRootDomain(string domain)
    {
        var parts = domain.Trim().ToLowerInvariant().Split('.');
        if (parts.Length >= 3)
            return string.Join(".", parts[^2..]); // Last 2 parts: domain.tld
        return domain.Trim().ToLowerInvariant();
    }

    private static void ExtractDomains(string text, Dictionary<string, (int Count, DomainSource Source)> domains)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        foreach (Match m in UrlDomainRegex.Matches(text))
        {
            var domain = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(domain)) continue;

            domain = ToRootDomain(domain);
            if (domain.Contains('.') && domain.Length > 3 && !IsFileExtension(domain))
            {
                var source = m.Groups[1].Success ? DomainSource.FromUrl : DomainSource.FromBare;
                AddOrUpgradeDomain(domains, domain, source);
            }
        }

        foreach (Match m in EmailDomainRegex.Matches(text))
        {
            var domain = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(domain)) continue;

            domain = ToRootDomain(domain);
            if (domain.Contains('.') && domain.Length > 3 && !IsFileExtension(domain))
                AddOrUpgradeDomain(domains, domain, DomainSource.FromEmail);
        }
    }

    private static void AddOrUpgradeDomain(
        Dictionary<string, (int Count, DomainSource Source)> domains,
        string domain,
        DomainSource source)
    {
        if (domains.TryGetValue(domain, out var existing))
        {
            var newSource = source > existing.Source ? source : existing.Source;
            domains[domain] = (existing.Count + 1, newSource);
        }
        else
        {
            domains[domain] = (1, source);
        }
    }

    private static string SelectBestDomain(Dictionary<string, (int Count, DomainSource Source)> domains)
    {
        return domains
            .OrderByDescending(x => x.Value.Source)
            .ThenByDescending(x => x.Value.Count)
            .ThenBy(x => x.Key)
            .First()
            .Key;
    }

    /// <summary>True if the string is a filename pattern like rechnung.pdf, not a real domain.</summary>
    private static bool IsFileExtension(string domain)
    {
        var tld = domain.AsSpan()[(domain.LastIndexOf('.') + 1)..];
        return tld.Length <= 5 && FileExtensionBlocklist.Contains(tld.ToString());
    }

    private static async Task<List<CorrespondentListItem>> GetAllCorrespondentsAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var list = new List<CorrespondentListItem>();
        var url = "api/correspondents/?page_size=100";

        while (url != null)
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"GET correspondents ({url}) failed: {response.StatusCode}");
            var json = await response.Content.ReadFromJsonAsync<CorrespondentsListResponse>(DeserializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (json?.Results == null) break;

            list.AddRange(json.Results);
            url = ToRelativeUrl(json.Next, client.BaseAddress);
        }

        return list;
    }

    private static async Task<List<int>> GetDocumentIdsByCorrespondentAsync(
        HttpClient client,
        int correspondentId,
        CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var url = $"api/documents/?correspondent__id={correspondentId}&page_size=100";

        while (url != null)
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<DocumentsListResponse>(DeserializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (json?.Results == null) break;

            foreach (var doc in json.Results)
                ids.Add(doc.Id);

            url = ToRelativeUrl(json.Next, client.BaseAddress);
        }

        return ids;
    }

    /// <summary>Convert absolute next URL to relative path so Auth header is sent with BaseAddress.</summary>
    private static string? ToRelativeUrl(string? next, Uri? baseAddress)
    {
        if (string.IsNullOrEmpty(next)) return null;
        if (!Uri.TryCreate(next, UriKind.Absolute, out var nextUri))
            return next;
        var pathAndQuery = nextUri.PathAndQuery.TrimStart('/');
        return string.IsNullOrEmpty(pathAndQuery) ? null : pathAndQuery;
    }

    private static async Task<(string Title, string Content, string FileName)> GetDocumentContentAsync(
        HttpClient client,
        int documentId,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"api/documents/{documentId}/", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<DocumentContentResponse>(DeserializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return (doc?.Title ?? "", doc?.Content ?? "", doc?.OriginalFileName ?? "");
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

    private sealed class CorrespondentsListResponse
    {
        [JsonPropertyName("results")]
        public List<CorrespondentListItem>? Results { get; set; }

        [JsonPropertyName("next")]
        public string? Next { get; set; }
    }

    private sealed class CorrespondentListItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("match")]
        public string? Match { get; set; }

        [JsonPropertyName("matching_algorithm")]
        public int MatchingAlgorithm { get; set; }
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

    private sealed class DocumentContentResponse
    {
        public string? Title { get; set; }
        public string? Content { get; set; }

        [JsonPropertyName("original_file_name")]
        public string? OriginalFileName { get; set; }
    }
}
