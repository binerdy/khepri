// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Abstracts the device camera. Implemented in Infrastructure (platform-specific).
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Captures a photo and returns the local file path of the saved image.
    /// When <paramref name="overlayImagePath"/> is provided, the live viewfinder
    /// shows that image as a semi-transparent ghost to help frame the shot.
    /// When <paramref name="destinationDir"/> is provided, the photo is saved
    /// into that directory; otherwise the implementation chooses a default location.
    /// </summary>
    Task<string> CapturePhotoAsync(
        string? overlayImagePath = null,
        string? destinationDir   = null,
        CancellationToken cancellationToken = default);
}
