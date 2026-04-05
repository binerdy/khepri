using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Stub — replace with MediaPipe Face Mesh alignment implementation.
/// </summary>
public sealed class MediaPipeFrameAlignmentService : IFrameAlignmentService
{
    public Task<string> AlignAsync(string referenceFilePath, string sourceFilePath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("MediaPipe alignment not yet implemented.");
}
