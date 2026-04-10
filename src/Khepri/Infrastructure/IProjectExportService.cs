// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Infrastructure;

/// <summary>
/// Zips a project folder and sends it to the OS share sheet as a .khepri file.
/// </summary>
public interface IProjectExportService
{
    Task ExportAsync(Guid projectId, string projectName);
}
