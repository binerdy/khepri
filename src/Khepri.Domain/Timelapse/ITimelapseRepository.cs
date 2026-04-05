// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Persistence boundary for timelapse projects.
/// Implemented in Infrastructure; consumed by Application.
/// </summary>
public interface ITimelapseRepository
{
    Task<IReadOnlyList<TimelapseProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TimelapseProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(TimelapseProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the filesystem path of the folder that contains all files for
    /// the given project. The folder may not exist yet.
    /// </summary>
    string GetProjectFolderPath(Guid id);
}
