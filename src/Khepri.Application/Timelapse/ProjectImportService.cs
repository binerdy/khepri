// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Text.Json;
using Khepri.Domain.Timelapse;

namespace Khepri.Application.Timelapse;

public sealed class ProjectImportService : IProjectImportService
{
    private readonly IStorageRootService _storageRoot;

    public ProjectImportService(IStorageRootService storageRoot)
        => _storageRoot = storageRoot;

    public async Task<IReadOnlyList<string>> ImportProjectsAsync(Stream zipStream)
    {
        return await Task.Run(() =>
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // Detect format: if project.json exists at the root it's the legacy
            // single-project format. Otherwise each top-level folder is one project.
            var rootManifest = archive.GetEntry("project.json");
            if (rootManifest is not null)
            {
                var name = ExtractSingleProject(archive, rootManifest);
                return name is null ? [] : [name];
            }

            // Multi-project format: entries like "{projectId}/project.json"
            var subManifests = archive.Entries
                .Where(e => !e.FullName.EndsWith('/')
                         && e.FullName.EndsWith("/project.json")
                         && e.FullName.Count(c => c == '/') == 1)
                .ToList();

            if (subManifests.Count == 0)
            {
                return [];
            }

            var results = new List<string>();
            foreach (var manifest in subManifests)
            {
                var folderPrefix = manifest.FullName[..manifest.FullName.LastIndexOf('/')];
                var name = ExtractProjectFromSubfolder(archive, manifest, folderPrefix);
                if (name is not null)
                {
                    results.Add(name);
                }
            }
            return results;
        });
    }

    // Legacy single-project: entries have no top-level folder prefix.
    private string? ExtractSingleProject(ZipArchive archive, ZipArchiveEntry manifestEntry)
    {
        if (!TryReadManifest(manifestEntry, out var projectId, out var projectName))
        {
            return null;
        }

        var destFolder = Path.Combine(_storageRoot.RootFolderPath, projectId.ToString());
        if (Directory.Exists(destFolder))
        {
            Directory.Delete(destFolder, recursive: true);
        }
        Directory.CreateDirectory(destFolder);
        archive.ExtractToDirectory(destFolder, overwriteFiles: true);
        return projectName;
    }

    // Multi-project: each project lives under a "{folderPrefix}/" top-level folder.
    private string? ExtractProjectFromSubfolder(
        ZipArchive archive, ZipArchiveEntry manifestEntry, string folderPrefix)
    {
        if (!TryReadManifest(manifestEntry, out var projectId, out var projectName))
        {
            return null;
        }

        var destFolder = Path.Combine(_storageRoot.RootFolderPath, projectId.ToString());
        if (Directory.Exists(destFolder))
        {
            Directory.Delete(destFolder, recursive: true);
        }
        Directory.CreateDirectory(destFolder);

        var prefix = folderPrefix + "/";
        foreach (var entry in archive.Entries.Where(
            e => e.FullName.StartsWith(prefix) && !e.FullName.EndsWith('/')))
        {
            var relativePath = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(destFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }
        return projectName;
    }

    private static bool TryReadManifest(
        ZipArchiveEntry manifestEntry, out Guid projectId, out string projectName)
    {
        projectId = Guid.Empty;
        projectName = "Imported Project";
        using var stream = manifestEntry.Open();
        var doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("Id", out var idEl) ||
            !idEl.TryGetGuid(out projectId))
        {
            return false;
        }
        if (doc.RootElement.TryGetProperty("Name", out var nameEl))
        {
            projectName = nameEl.GetString() ?? "Imported Project";
        }
        return true;
    }
}
