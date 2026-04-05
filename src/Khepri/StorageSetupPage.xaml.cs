// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Domain.Timelapse;

namespace Khepri;

public partial class StorageSetupPage : ContentPage
{
    private readonly IStorageRootService _storageRoot;
    private readonly AppShell            _shell;

    // True after we open the Android Settings page for All Files Access.
    // OnAppearing fires when the user returns so we can auto-advance.
    private bool _waitingForPermission;

    public StorageSetupPage(IStorageRootService storageRoot, AppShell shell)
    {
        _storageRoot = storageRoot;
        _shell       = shell;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_waitingForPermission)
        {
            if (_storageRoot.HasRootFolder)
            {
                _waitingForPermission = false;
                NavigateToShell();
            }
            else
            {
                SetStatus("Permission was not granted. Tap 'Grant Access' to try again.");
                ActionButton.Text      = "Grant Access";
                ActionButton.IsEnabled = true;
            }
        }
    }

    private async void OnActionButtonClicked(object? sender, EventArgs e)
    {
        ActionButton.IsEnabled = false;
        SetStatus("Requesting permission…");

        var granted = await _storageRoot.RequestRootFolderAsync();

        if (granted)
        {
            NavigateToShell();
        }
        else
        {
            // API 30+: opened Settings; detect grant in OnAppearing
            _waitingForPermission  = true;
            ActionButton.Text      = "I've Granted Access";
            ActionButton.IsEnabled = true;
            SetStatus("Enable 'All Files Access' for Khepri in Settings, then return here.");
        }
    }

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
