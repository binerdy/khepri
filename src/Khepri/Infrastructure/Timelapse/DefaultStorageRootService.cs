// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Fallback implementation for non-Android platforms.
/// Uses the MAUI app-data directory (no user interaction required).
/// </summary>
public sealed class DefaultStorageRootService : IStorageRootService
{
    public bool HasRootFolder => true;

    public string RootFolderPath => FileSystem.AppDataDirectory;

    public Task<bool> RequestRootFolderAsync() => Task.FromResult(true);
}
