// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Application.Timelapse;

public interface IVideoExportService
{
    /// <summary>
    /// Encodes the provided JPEG frames into an H.264/MP4 file and returns its path.
    /// </summary>
    Task<string> ExportAsync(
        IReadOnlyList<string> framePaths,
        double secondsPerFrame,
        TransitionEffect transition,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
