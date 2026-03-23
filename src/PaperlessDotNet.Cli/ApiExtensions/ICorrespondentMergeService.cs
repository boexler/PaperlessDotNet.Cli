namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Merges a source correspondent into a target correspondent by moving all
/// documents to the target and deleting the source.
/// </summary>
public interface ICorrespondentMergeService
{
    /// <summary>
    /// Merges source correspondent into target: reassigns all documents to target, then deletes source.
    /// </summary>
    /// <param name="sourceId">Correspondent ID to merge away (will be deleted).</param>
    /// <param name="targetId">Correspondent ID to keep (documents will be reassigned here).</param>
    /// <param name="baseUrl">Paperless base URL, or null for default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of documents reassigned.</returns>
    Task<int> MergeAsync(int sourceId, int targetId, Uri? baseUrl, CancellationToken cancellationToken = default);
}
