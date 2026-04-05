namespace Khepri.Domain.Timelapse;

/// <summary>
/// Abstracts face-landmark-based frame alignment. Implemented in Infrastructure.
/// </summary>
public interface IFrameAlignmentService
{
    /// <summary>
    /// Aligns <paramref name="sourceFilePath"/> to the reference frame and saves the result.
    /// Returns the file path of the aligned image.
    /// </summary>
    Task<string> AlignAsync(string referenceFilePath, string sourceFilePath, CancellationToken cancellationToken = default);
}
