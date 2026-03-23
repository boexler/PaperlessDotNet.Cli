// WORKAROUND: Tag/Correspondent Update via direct HTTP PATCH.
// PaperlessDotNet does not yet expose Update. Remove this module and switch
// to client.Tags.Update / client.Correspondents.Update when available.

namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Temporary service for updating tags and correspondents via direct HTTP PATCH.
/// Remove when PaperlessDotNet adds Update support.
/// </summary>
public interface ITagCorrespondentUpdateService
{
    /// <summary>
    /// Updates a tag via PATCH /api/tags/{id}/.
    /// </summary>
    /// <param name="id">Tag ID.</param>
    /// <param name="patch">Fields to update (partial).</param>
    /// <param name="baseUrl">Optional base URL for multi-server setups.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated tag as returned by API.</returns>
    Task<TagPatchResponse> UpdateTagAsync(
        int id,
        TagPatchDto patch,
        Uri? baseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a correspondent via PATCH /api/correspondents/{id}/.
    /// </summary>
    /// <param name="id">Correspondent ID.</param>
    /// <param name="patch">Fields to update (partial).</param>
    /// <param name="baseUrl">Optional base URL for multi-server setups.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated correspondent as returned by API.</returns>
    Task<CorrespondentPatchResponse> UpdateCorrespondentAsync(
        int id,
        CorrespondentPatchDto patch,
        Uri? baseUrl,
        CancellationToken cancellationToken = default);
}
