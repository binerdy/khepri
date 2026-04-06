// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Fallback for non-Android platforms where Bitmap transforms are unavailable.
/// Returns the source path unchanged so playback still works without alignment.
/// </summary>
public sealed class StubImageTransformService : IImageTransformService
{
    public Task<string> ApplyTranslationAsync(
        string sourcePath,
        float translateX,
        float translateY,
        CancellationToken cancellationToken = default)
        => Task.FromResult(sourcePath);
}
