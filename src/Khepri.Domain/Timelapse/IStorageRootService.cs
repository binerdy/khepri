// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Khepri.Domain.Timelapse;

/// <summary>
/// Provides the root folder used for all project + photo storage.
/// </summary>
public interface IStorageRootService
{
    /// <summary>
    /// Absolute path of the root folder. The directory is created on first access.
    /// </summary>
    string RootFolderPath { get; }
}
