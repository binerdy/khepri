// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.App;
using Android.Content;
using Android.Content.PM;
using Khepri.Platforms.Android;

namespace Khepri;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == AndroidStorageRootService.FolderPickerRequestCode)
        {
            var uri = resultCode == Result.Ok ? data?.Data : null;
            AndroidStorageRootService.HandleFolderPickerResult(uri);
        }
    }
}
