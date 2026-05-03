// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Application.Timelapse;

/// <summary>
/// All per-frame data needed to composite a frame into the exported video.
/// </summary>
/// <param name="FilePath">Absolute path to the JPEG (AlignedFilePath ?? FilePath).</param>
/// <param name="OffsetX">Horizontal alignment offset in dp.</param>
/// <param name="OffsetY">Vertical alignment offset in dp.</param>
/// <param name="Rotation">Rotation in degrees (clockwise).</param>
/// <param name="Scale">Uniform user scale factor (1.0 = no scale).</param>
/// <param name="ReferenceViewWidth">
/// dp width of the alignment viewer at save time. Used to convert the dp offset to
/// video pixel space. Zero means unknown — the encoder uses a 360 dp fallback.
/// </param>
public readonly record struct FrameRenderInfo(
    string FilePath,
    double OffsetX,
    double OffsetY,
    double Rotation,
    double Scale,
    double ReferenceViewWidth);

public interface IVideoExportService
{
    /// <summary>
    /// Encodes the provided frames into an H.264/MP4 file and returns its path.
    /// Each frame is rendered with AspectFit centering and the stored alignment
    /// transforms applied, exactly matching the playback screen appearance.
    /// </summary>
    Task<string> ExportAsync(
        IReadOnlyList<FrameRenderInfo> frames,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
