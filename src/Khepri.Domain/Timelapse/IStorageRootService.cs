// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Provides and persists the root folder used for all project + photo storage.
/// The chosen path survives app reinstall because it lives in user-accessible
/// external storage rather than the app-private sandbox.
/// </summary>
public interface IStorageRootService
{
    /// <summary>
    /// Returns <see langword="true"/> when the user has already selected a
    /// root folder (either on this launch or a previous one).
    /// </summary>
    bool HasRootFolder { get; }

    /// <summary>
    /// Absolute path of the selected root folder.
    /// Throws if <see cref="HasRootFolder"/> is <see langword="false"/>.
    /// </summary>
    string RootFolderPath { get; }

    /// <summary>
    /// Guides the user through granting storage permission and selecting a
    /// folder.  Returns <see langword="true"/> on success.
    /// </summary>
    Task<bool> RequestRootFolderAsync();
}
