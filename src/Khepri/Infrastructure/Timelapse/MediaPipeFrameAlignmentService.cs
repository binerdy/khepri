// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
