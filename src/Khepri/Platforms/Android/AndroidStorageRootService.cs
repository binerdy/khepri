// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.Content;
using Khepri.Domain.Timelapse;
using AndroidEnvironment = Android.OS.Environment;
using AndroidSettings = Android.Provider.Settings;

namespace Khepri.Platforms.Android;

/// <summary>
/// Android implementation of <see cref="IStorageRootService"/>.
///
/// Storage root is fixed to <c>Pictures/Khepri</c> on external storage:
///   /storage/emulated/0/Pictures/Khepri/
///
/// Storage strategy by API level:
///   API 21–28  → WRITE_EXTERNAL_STORAGE runtime permission
///   API 29     → WRITE_EXTERNAL_STORAGE + requestLegacyExternalStorage in manifest
///   API 30+    → MANAGE_EXTERNAL_STORAGE (All Files Access via Settings)
///
/// On first launch (or after reinstall), <see cref="HasRootFolder"/> returns false
/// and the app shows <see cref="Khepri.StorageSetupPage"/> to obtain permission.
/// After that the path is always available without any user interaction.
/// </summary>
public sealed class AndroidStorageRootService : IStorageRootService
{
    // Preferences key used on API ≤ 29 to remember that we have already obtained
    // WRITE_EXTERNAL_STORAGE (runtime permission).  Not needed on API 30+ because
    // IsExternalStorageManager is the live source of truth.
    private const string PermissionGrantedKey = "storage_permission_granted";

    // ── Fixed storage root ────────────────────────────────────────────────────

    private static string PicturesKhepriPath =>
        Path.Combine(
            AndroidEnvironment.GetExternalStoragePublicDirectory(
                AndroidEnvironment.DirectoryPictures)!.AbsolutePath,
            "Khepri");

    // ── IStorageRootService ───────────────────────────────────────────────────

    public bool HasRootFolder =>
        OperatingSystem.IsAndroidVersionAtLeast(30)
            ? AndroidEnvironment.IsExternalStorageManager
            : Preferences.Get(PermissionGrantedKey, false);

    public string RootFolderPath
    {
        get
        {
            var path = PicturesKhepriPath;
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public async Task<bool> RequestRootFolderAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (AndroidEnvironment.IsExternalStorageManager)
            {
                Directory.CreateDirectory(PicturesKhepriPath);
                return true;
            }

            // Open the "All Files Access" settings page for this app.
            // Returns false — StorageSetupPage re-checks in OnAppearing.
            var intent = new Intent(
                AndroidSettings.ActionManageAppAllFilesAccessPermission,
                global::Android.Net.Uri.Parse(
                    "package:" + Platform.CurrentActivity!.PackageName));
            Platform.CurrentActivity.StartActivity(intent);
            return false;
        }
        else
        {
            var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            if (status != PermissionStatus.Granted)
            {
                return false;
            }

            Preferences.Set(PermissionGrantedKey, true);
            Directory.CreateDirectory(PicturesKhepriPath);
            return true;
        }
    }

    /// <summary>True once All Files Access is confirmed granted (API 30+ only).</summary>
    internal static bool IsExternalStorageManagerGranted =>
        OperatingSystem.IsAndroidVersionAtLeast(30) && AndroidEnvironment.IsExternalStorageManager;
}
