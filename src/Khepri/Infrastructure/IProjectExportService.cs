// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Infrastructure;

/// <summary>
/// Zips one or more project folders into a single .khepri file and sends it to the OS share sheet.
/// </summary>
public interface IProjectExportService
{
    Task ExportAsync(IReadOnlyList<(Guid Id, string Name)> projects);
}
