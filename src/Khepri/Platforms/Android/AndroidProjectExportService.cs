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

    public async Task ExportAsync(Guid projectId, string projectName)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current Activity.");

        var projectFolder = _repository.GetProjectFolderPath(projectId);
        if (!Directory.Exists(projectFolder))
        {
            throw new DirectoryNotFoundException($"Project folder not found: {projectFolder}");
        }

        // Write the zip to the cache sub-directory declared in file_paths.xml
        var exportsDir = Path.Combine(Platform.CurrentActivity!.CacheDir!.AbsolutePath, "exports");
        Directory.CreateDirectory(exportsDir);

        var safeName = string.Concat(projectName
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')
            ).Trim().Replace(' ', '_');
        var zipPath = Path.Combine(exportsDir, $"{safeName}.khepri");

        // Build the zip on a background thread — frames can be large
        await Task.Run(() =>
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(projectFolder, zipPath,
                CompressionLevel.Fastest, includeBaseDirectory: false);
        });

        // Obtain a content:// URI via FileProvider
        var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            activity,
            activity.PackageName + ".fileprovider",
            new Java.IO.File(zipPath));

        var shareIntent = new Intent(Intent.ActionSend);
        shareIntent.SetType("application/octet-stream");
        shareIntent.PutExtra(Intent.ExtraStream, fileUri);
        shareIntent.PutExtra(Intent.ExtraSubject, $"Khepri project: {projectName}");
        shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

        activity.StartActivity(Intent.CreateChooser(shareIntent, $"Export \u201c{projectName}\u201d"));
    }
}
