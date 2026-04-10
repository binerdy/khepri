// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;

namespace Khepri;

[Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionSend],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "application/octet-stream",
    Label = "Import into Khepri")]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Raised when the activity receives a .khepri import intent.
    /// MainPage subscribes to trigger the import flow.
    /// </summary>
    public static event Action<Stream>? ImportRequested;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Pass null to wipe all saved fragment state (both android:support:fragments
        // and the nested SavedStateRegistry path). Stale fragments referencing
        // id/left (flyout split pane) crash on start; Shell rebuilds cleanly.
        base.OnCreate(savedInstanceState: null);
        HandleImportIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleImportIntent(intent);
    }

    private static void HandleImportIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend)
        {
            return;
        }

        if (IntentCompat.GetParcelableExtra(intent, Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) is not Android.Net.Uri uri)
        {
            return;
        }

        var resolver = Android.App.Application.Context.ContentResolver;
        var stream = resolver?.OpenInputStream(uri);
        if (stream is null)
        {
            return;
        }

        ImportRequested?.Invoke(stream);
    }
}

