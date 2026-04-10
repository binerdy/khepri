// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IStorageRootService"/>.
/// Uses app-private internal storage — no permissions required.
/// Projects are accessible to the user via Export/Import.
/// </summary>
public sealed class AndroidStorageRootService : IStorageRootService
{
    public string RootFolderPath
    {
        get
        {
            var path = FileSystem.AppDataDirectory;
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
