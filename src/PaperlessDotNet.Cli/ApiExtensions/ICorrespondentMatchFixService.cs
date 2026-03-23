namespace PaperlessDotNet.Cli.ApiExtensions;

/// <summary>
/// Fixes correspondent Match fields by extracting URL domains from associated documents
/// and setting them as regex patterns for auto-assignment.
/// </summary>
public interface ICorrespondentMatchFixService
{
    /// <summary>
    /// Finds correspondents without regex match, analyzes their documents for URL domains,
    /// and sets the most frequent domain as Match with MatchingAlgorithm=Regex.
    /// </summary>
    /// <param name="dryRun">If true, only outputs suggestions without applying updates.</param>
    /// <param name="requireRegex">If true, fix all with MatchingAlgorithm != 4. If false, only fix empty Match.</param>
    /// <param name="baseUrl">Paperless base URL, or null for stored default.</param>
    /// <param name="correspondentId">If set, only process this correspondent. If null, process all.</param>
    /// <param name="progress">Optional progress reporter for each result as it completes.</param>
    /// <param name="onSkippedNoDomains">When set, invoked with (correspondentId, correspondentName, searchTextPreview) when no URL domain found. Use for --verbose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of fix results for reporting.</returns>
    Task<IReadOnlyList<CorrespondentMatchFixResult>> FixMatchAsync(
        bool dryRun,
        bool requireRegex,
        Uri? baseUrl,
        int? correspondentId = null,
        IProgress<CorrespondentMatchFixResult>? progress = null,
        Action<int, string, string>? onSkippedNoDomains = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a single correspondent match fix attempt.
/// </summary>
/// <param name="CorrespondentId">Correspondent ID.</param>
/// <param name="CorrespondentName">Correspondent name.</param>
/// <param name="SuggestedMatch">The regex pattern that was or would be set.</param>
/// <param name="Status">Whether the fix was applied, skipped, or dry-run.</param>
/// <param name="Message">Human-readable detail (e.g., reason for skip).</param>
public sealed record CorrespondentMatchFixResult(
    int CorrespondentId,
    string CorrespondentName,
    string? SuggestedMatch,
    CorrespondentMatchFixStatus Status,
    string Message);

/// <summary>
/// Status of a correspondent match fix.
/// </summary>
public enum CorrespondentMatchFixStatus
{
    /// <summary>Fix was applied successfully.</summary>
    Applied,

    /// <summary>Dry run: suggestion only, no update.</summary>
    DryRun,

    /// <summary>Skipped (e.g., no documents, no domain found).</summary>
    Skipped,
}
