// WORKAROUND: Tag/Correspondent Update via direct HTTP PATCH.
// PaperlessDotNet does not yet expose Update. Remove this module and switch
// to client.Tags.Update / client.Correspondents.Update when available.

using System.Text.Json.Serialization;

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// DTO for PATCH /api/correspondents/{id}/ request body.
/// All properties optional for partial updates.
/// </summary>
public sealed class CorrespondentPatchDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Match { get; init; }

    /// <summary>
    /// 0=None, 1=Any word, 2=All words, 3=Exact, 4=Regex, 5=Fuzzy, 6=Automatic.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MatchingAlgorithm { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsInsensitive { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Owner { get; init; }
}
