// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Storage root for non-Android platforms — uses the MAUI app-data directory.
/// </summary>
public sealed class DefaultStorageRootService : IStorageRootService
{
    public string RootFolderPath => FileSystem.AppDataDirectory;
}
