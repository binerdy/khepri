// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Khepri;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Pass null to wipe all saved fragment state (both android:support:fragments
        // and the nested SavedStateRegistry path). Stale fragments referencing
        // id/left (flyout split pane) crash on start; Shell rebuilds cleanly.
        base.OnCreate(savedInstanceState: null);
    }
}
