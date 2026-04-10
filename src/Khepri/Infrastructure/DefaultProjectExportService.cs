// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Khepri.Infrastructure.Timelapse;

namespace Khepri.Infrastructure;

/// <summary>Non-Android export using MAUI's cross-platform Share API.</summary>
public sealed class DefaultProjectExportService : IProjectExportService
{
    private readonly JsonTimelapseRepository _repository;

    public DefaultProjectExportService(JsonTimelapseRepository repository)
        => _repository = repository;

    public async Task ExportAsync(Guid projectId, string projectName)
    {
        var projectFolder = _repository.GetProjectFolderPath(projectId);
        if (!Directory.Exists(projectFolder))
        {
            throw new DirectoryNotFoundException($"Project folder not found: {projectFolder}");
        }

        var cacheDir = FileSystem.CacheDirectory;
        var exportsDir = Path.Combine(cacheDir, "exports");
        Directory.CreateDirectory(exportsDir);

        var safeName = string.Concat(projectName
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')
            ).Trim().Replace(' ', '_');
        var zipPath = Path.Combine(exportsDir, $"{safeName}.khepri");

        await Task.Run(() =>
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(projectFolder, zipPath,
                CompressionLevel.Fastest, includeBaseDirectory: false);
        });

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"Export \u201c{projectName}\u201d",
            File = new ShareFile(zipPath, "application/octet-stream")
        });
    }
}
