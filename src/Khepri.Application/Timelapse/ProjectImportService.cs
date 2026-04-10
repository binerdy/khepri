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

    public async Task<string?> ImportAsync(Stream zipStream)
    {
        return await Task.Run(() =>
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // Validate: must contain a project.json
            var manifestEntry = archive.GetEntry("project.json");
            if (manifestEntry is null)
            {
                return null;
            }

            // Read the project ID and name from the manifest without extracting everything
            Guid projectId;
            string projectName;
            using (var manifestStream = manifestEntry.Open())
            {
                var doc = JsonDocument.Parse(manifestStream);
                if (!doc.RootElement.TryGetProperty("Id", out var idEl) ||
                    !idEl.TryGetGuid(out projectId))
                {
                    return null;
                }
                projectName = doc.RootElement.TryGetProperty("Name", out var nameEl)
                    ? nameEl.GetString() ?? "Imported Project"
                    : "Imported Project";
            }

            var destFolder = Path.Combine(_storageRoot.RootFolderPath, projectId.ToString());

            // If a project with the same ID already exists, overwrite it
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, recursive: true);
            }
            Directory.CreateDirectory(destFolder);

            archive.ExtractToDirectory(destFolder, overwriteFiles: true);
            return projectName;
        });
    }
}
