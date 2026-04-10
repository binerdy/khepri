// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Application.Timelapse;

/// <summary>
/// Reads a .khepri zip file and extracts it into the app's project storage root.
/// Returns the name of the imported project, or null if the zip is invalid.
/// </summary>
public interface IProjectImportService
{
    Task<string?> ImportAsync(Stream zipStream);
}
