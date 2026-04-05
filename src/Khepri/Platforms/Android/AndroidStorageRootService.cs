// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.Content;
using Android.Provider;
using Khepri.Domain.Timelapse;
using AndroidEnvironment = Android.OS.Environment;
using AndroidSettings = Android.Provider.Settings;

namespace Khepri.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IStorageRootService"/>.
///
/// Storage strategy by API level:
///   API 21–28  → WRITE_EXTERNAL_STORAGE runtime permission  → System.IO works
///   API 29     → requestLegacyExternalStorage in manifest   → System.IO works
///   API 30+    → MANAGE_EXTERNAL_STORAGE (All Files Access) → System.IO works
///
/// The user picks the root folder once via ACTION_OPEN_DOCUMENT_TREE.
/// The resolved filesystem path is persisted in Preferences so it survives
/// app restarts (but not uninstall — the user simply picks the same folder
/// again after reinstalling, and all previously saved files are still there).
/// </summary>
public sealed class AndroidStorageRootService : IStorageRootService
{
    private const string PrefKey = "storage_root_path";
    internal const int FolderPickerRequestCode = 2001;

    private static TaskCompletionSource<global::Android.Net.Uri?>? _folderTcs;

    // Called by MainActivity.OnActivityResult
    internal static void HandleFolderPickerResult(global::Android.Net.Uri? uri)
        => Interlocked.Exchange(ref _folderTcs, null)?.TrySetResult(uri);

    // ── IStorageRootService ───────────────────────────────────────────────────

    private string? _cached;

    public bool HasRootFolder
    {
        get
        {
            if (_cached is not null)
            {
                return true;
            }

            var saved = Preferences.Get(PrefKey, null as string);
            if (saved is null)
            {
                return false;
            }

            _cached = saved;
            return true;
        }
    }

    public string RootFolderPath
    {
        get
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var saved = Preferences.Get(PrefKey, null as string)
                ?? throw new InvalidOperationException(
                    "No root folder has been selected. Call RequestRootFolderAsync first.");
            _cached = saved;
            return _cached;
        }
    }

    public async Task<bool> RequestRootFolderAsync()
    {
        if (!await EnsureStoragePermissionAsync())
        {
            return false;
        }

        return await PickFolderAsync();
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// On API ≥ 30 opens the "All Files Access" settings page for this app.
    /// On API &lt; 30 requests the legacy WRITE_EXTERNAL_STORAGE permission.
    /// Returns true when the permission is currently granted.
    /// </summary>
    internal static async Task<bool> EnsureStoragePermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (AndroidEnvironment.IsExternalStorageManager)
            {
                return true;
            }

            // Open the "All Files Access" settings page
            var intent = new Intent(
                AndroidSettings.ActionManageAppAllFilesAccessPermission,
                global::Android.Net.Uri.Parse(
                    "package:" + Platform.CurrentActivity!.PackageName));
            Platform.CurrentActivity.StartActivity(intent);

            // Return false — caller (StorageSetupPage) will re-check on resume
            return false;
        }
        else
        {
            var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            return status == PermissionStatus.Granted;
        }
    }

    /// <summary>Launches the SAF folder picker and resolves the result to a path.</summary>
    internal async Task<bool> PickFolderAsync()
    {
        _folderTcs = new TaskCompletionSource<global::Android.Net.Uri?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission
                       | ActivityFlags.GrantWriteUriPermission
                       | ActivityFlags.GrantPersistableUriPermission);

        Platform.CurrentActivity!.StartActivityForResult(intent, FolderPickerRequestCode);

        var uri = await _folderTcs.Task;
        if (uri is null)
        {
            return false;
        }

        // Take persistable permission so the grant survives app restart
        Platform.CurrentActivity.ContentResolver?.TakePersistableUriPermission(
            uri,
            ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

        var path = ResolveDocumentTreeUri(uri);
        if (path is null)
        {
            return false;
        }

        Directory.CreateDirectory(path);
        Preferences.Set(PrefKey, path);
        _cached = path;
        return true;
    }

    /// <summary>Checks if All Files Access is currently granted (API 30+ only).</summary>
    internal static bool IsExternalStorageManagerGranted =>
        OperatingSystem.IsAndroidVersionAtLeast(30) && AndroidEnvironment.IsExternalStorageManager;

    // ── URI → filesystem path ────────────────────────────────────────────────

    private static string? ResolveDocumentTreeUri(global::Android.Net.Uri treeUri)
    {
        // DocumentsContract returns e.g. "primary:Documents/Khepri"
        var docId = DocumentsContract.GetTreeDocumentId(treeUri);
        if (docId is null)
        {
            return null;
        }

        var colonIdx = docId.IndexOf(':');
        if (colonIdx < 0)
        {
            return null;
        }

        var volume       = docId[..colonIdx];
        var relativePath = Uri.UnescapeDataString(docId[(colonIdx + 1)..]);

        string basePath = string.Equals(volume, "primary", StringComparison.OrdinalIgnoreCase)
            ? AndroidEnvironment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0"
            : $"/storage/{volume}";

        return string.IsNullOrEmpty(relativePath)
            ? basePath
            : Path.Combine(basePath, relativePath);
    }
}
