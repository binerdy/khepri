// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Applies a pixel-level translation to a source image and persists the result.
/// </summary>
public interface IImageTransformService
{
    /// <summary>
    /// Translates <paramref name="sourcePath"/> by (<paramref name="translateX"/>, <paramref name="translateY"/>)
    /// pixels, saves the result next to the original (suffix <c>_aligned</c>) and returns the output path.
    /// </summary>
    Task<string> ApplyTranslationAsync(
        string sourcePath,
        float translateX,
        float translateY,
        CancellationToken cancellationToken = default);
}
