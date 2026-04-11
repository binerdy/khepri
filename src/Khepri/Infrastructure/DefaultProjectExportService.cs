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

    public async Task ExportAsync(IReadOnlyList<(Guid Id, string Name)> projects)
    {
        if (projects.Count == 0)
        {
            return;
        }

        var exportsDir = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(exportsDir);

        var zipFileName = projects.Count == 1
            ? Sanitize(projects[0].Name) + ".khepri"
            : $"khepri_{projects.Count}_projects.khepri";
        var zipPath = Path.Combine(exportsDir, zipFileName);

        await Task.Run(() =>
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var (id, _) in projects)
            {
                var folder = _repository.GetProjectFolderPath(id);
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var relPath = Path.GetRelativePath(folder, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, $"{id}/{relPath}", CompressionLevel.Fastest);
                }
            }
        });

        var title = projects.Count == 1
            ? $"Export \u201c{projects[0].Name}\u201d"
            : $"Export {projects.Count} projects";

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(zipPath, "application/octet-stream")
        });
    }

    private static string Sanitize(string name)
        => string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' '))
               .Trim().Replace(' ', '_');
}
