// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri;

public partial class StorageSetupPage : ContentPage
{
    private readonly IStorageRootService _storageRoot;
    private readonly AppShell            _shell;

    // Set to true after we send the user to Android Settings for All Files Access.
    // OnAppearing fires when they return and we can auto-continue.
    private bool _waitingForAllFilesAccess;

    public StorageSetupPage(IStorageRootService storageRoot, AppShell shell)
    {
        _storageRoot = storageRoot;
        _shell       = shell;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Return from "All Files Access" settings — check if granted and auto-continue
        if (_waitingForAllFilesAccess)
        {
#if ANDROID
            if (Khepri.Platforms.Android.AndroidStorageRootService.IsExternalStorageManagerGranted)
            {
                _waitingForAllFilesAccess = false;
                await PickFolderAndAdvanceAsync();
            }
            else
            {
                SetStatus("Permission was not granted. Please enable 'All Files Access' for Khepri.");
                ActionButton.Text    = "Try Again";
                ActionButton.IsEnabled = true;
            }
#endif
        }
    }

    private async void OnActionButtonClicked(object? sender, EventArgs e)
    {
        ActionButton.IsEnabled = false;

        if (_waitingForAllFilesAccess)
        {
            // Re-check permission after user returned without granting
#if ANDROID
            if (!Khepri.Platforms.Android.AndroidStorageRootService.IsExternalStorageManagerGranted)
            {
                await RequestAllFilesAccessAsync();
                return;
            }
            _waitingForAllFilesAccess = false;
            await PickFolderAndAdvanceAsync();
#endif
            return;
        }

#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(30) &&
            !Khepri.Platforms.Android.AndroidStorageRootService.IsExternalStorageManagerGranted)
        {
            await RequestAllFilesAccessAsync();
            return;
        }
#endif

        await PickFolderAndAdvanceAsync();
    }

    private async Task PickFolderAndAdvanceAsync()
    {
        SetStatus("Opening folder picker…");
        ActionButton.IsEnabled = false;

#if ANDROID
        var androidService = (Khepri.Platforms.Android.AndroidStorageRootService)_storageRoot;

        var basePath = await Khepri.Platforms.Android.AndroidStorageRootService.PickFolderPathAsync();
        if (basePath is null)
        {
            SetStatus("No folder was selected. Please try again.");
            ActionButton.Text      = "Select Storage Folder";
            ActionButton.IsEnabled = true;
            return;
        }

        // Ask if the user wants to create a subfolder within the selected location
        var subfolderName = await DisplayPromptAsync(
            "Create Subfolder?",
            $"Projects will be saved in:\n{basePath}\n\nEnter a subfolder name to create one, or leave blank to use this folder directly.",
            "Use This Folder",
            "Cancel",
            placeholder: "Khepri",
            initialValue: "Khepri");

        // null means Cancel was tapped
        if (subfolderName is null)
        {
            SetStatus("No folder was selected. Please try again.");
            ActionButton.Text      = "Select Storage Folder";
            ActionButton.IsEnabled = true;
            return;
        }

        var finalPath = string.IsNullOrWhiteSpace(subfolderName)
            ? basePath
            : Path.Combine(basePath, subfolderName.Trim());

        androidService.SaveRootPath(finalPath);
        NavigateToShell();
#else
        var success = await _storageRoot.RequestRootFolderAsync();

        if (success)
        {
            NavigateToShell();
        }
        else
        {
            SetStatus("No folder was selected. Please try again.");
            ActionButton.Text      = "Select Storage Folder";
            ActionButton.IsEnabled = true;
        }
#endif
    }

#if ANDROID
    private async Task RequestAllFilesAccessAsync()
    {
        bool agreed = await DisplayAlertAsync(
            "All Files Access Required",
            "Khepri needs 'All Files Access' so your projects survive uninstall. " +
            "You will be taken to Settings — please enable it for Khepri, then return here.",
            "Open Settings",
            "Cancel");

        if (!agreed)
        {
            ActionButton.IsEnabled = true;
            return;
        }

        _waitingForAllFilesAccess = true;
        SetStatus("Waiting for permission… Return here after enabling 'All Files Access'.");
        ActionButton.Text      = "I've Granted Access";
        ActionButton.IsEnabled = true;

        // This opens Settings and returns immediately; we detect the grant in OnAppearing
        await Khepri.Platforms.Android.AndroidStorageRootService.EnsureStoragePermissionAsync();
    }
#endif

    private void NavigateToShell()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows is [{ } window, ..])
        {
            window.Page = _shell;
        }
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text      = message;
        StatusLabel.IsVisible = !string.IsNullOrEmpty(message);
    }
}
