// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Application.Timelapse;

/// <summary>
/// Reads a .khepri zip file and extracts all contained projects into the app's storage root.
/// Supports both single-project (legacy) and multi-project zip formats.
/// Returns the names of imported projects, or an empty list if the zip is invalid.
/// </summary>
public interface IProjectImportService
{
    Task<IReadOnlyList<string>> ImportProjectsAsync(Stream zipStream);
}
