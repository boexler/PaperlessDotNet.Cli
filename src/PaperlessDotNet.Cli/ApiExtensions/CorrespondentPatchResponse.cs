// WORKAROUND: Tag/Correspondent Update via direct HTTP PATCH.
// PaperlessDotNet does not yet expose Update. Remove this module and switch
// to client.Tags.Update / client.Correspondents.Update when available.

using System.Text.Json.Serialization;

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Response shape for PATCH /api/correspondents/{id}/.
/// </summary>
public sealed class CorrespondentPatchResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = null!;

    [JsonPropertyName("match")]
    public string? Match { get; set; }

    [JsonPropertyName("matching_algorithm")]
    public int? MatchingAlgorithm { get; set; }

    [JsonPropertyName("is_insensitive")]
    public bool? IsInsensitive { get; set; }

    [JsonPropertyName("document_count")]
    public int? DocumentCount { get; set; }

    [JsonPropertyName("owner")]
    public int? Owner { get; set; }
}
