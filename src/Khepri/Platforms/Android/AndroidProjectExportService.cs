// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Android.Content;
using Khepri.Infrastructure;
using Khepri.Infrastructure.Timelapse;

namespace Khepri.Platforms.Android;

public sealed class AndroidProjectExportService : IProjectExportService
{
    private readonly JsonTimelapseRepository _repository;

    public AndroidProjectExportService(JsonTimelapseRepository repository)
        => _repository = repository;

    public async Task ExportAsync(IReadOnlyList<(Guid Id, string Name)> projects)
    {
        if (projects.Count == 0)
        {
            return;
        }

        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Activity.");

        var zipPath = await BuildZipAsync(projects, activity.CacheDir!.AbsolutePath);

        var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            activity,
            activity.PackageName + ".fileprovider",
            new Java.IO.File(zipPath));

        var title = projects.Count == 1
            ? $"Export \u201c{projects[0].Name}\u201d"
            : $"Export {projects.Count} projects";

        var shareIntent = new Intent(Intent.ActionSend);
        shareIntent.SetType("application/octet-stream");
        shareIntent.PutExtra(Intent.ExtraStream, fileUri);
        shareIntent.PutExtra(Intent.ExtraSubject, title);
        shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        activity.StartActivity(Intent.CreateChooser(shareIntent, title));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> BuildZipAsync(IReadOnlyList<(Guid Id, string Name)> projects, string cacheRootDir)
    {
        var exportsDir = Path.Combine(cacheRootDir, "exports");
        Directory.CreateDirectory(exportsDir);

        var zipFileName = projects.Count == 1
            ? Sanitize(projects[0].Name) + ".khepri"
            : $"khepri_{projects.Count}_projects.khepri";
        var zipPath = Path.Combine(exportsDir, zipFileName);

        // Build the zip on a background thread — frames can be large.
        // Each project goes in its own {projectId}/ subfolder so multiple projects
        // can coexist in one archive.
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

        return zipPath;
    }

    private static string Sanitize(string name)
        => string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' '))
               .Trim().Replace(' ', '_');
}
