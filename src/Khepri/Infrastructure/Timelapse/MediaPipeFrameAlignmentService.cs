// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Non-Android fallback — alignment is not supported on this platform.
/// The real implementation lives in Platforms/Android/MediaPipeFrameAlignmentService.cs.
/// </summary>
public sealed class MediaPipeFrameAlignmentService : IFrameAlignmentService
{
    public Task<string> AlignAsync(
        string referenceFilePath,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Frame alignment is only supported on Android.");
}
