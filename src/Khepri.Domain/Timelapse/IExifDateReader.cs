// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Reads the capture date from image file metadata (EXIF).
/// Implemented in Infrastructure so the Application layer stays free of I/O details.
/// </summary>
public interface IExifDateReader
{
    /// <summary>
    /// Returns the <c>DateTimeOriginal</c> EXIF tag from <paramref name="filePath"/>,
    /// falling back to the <c>DateTime</c> tag. Returns <see langword="null"/> when no
    /// EXIF date is present or the file cannot be parsed.
    /// </summary>
    DateTimeOffset? ReadDateTaken(string filePath);
}
