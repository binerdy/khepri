// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class TimelapsePreviewPage : ContentPage
{
    private readonly TimelapsePreviewViewModel _vm;

    public TimelapsePreviewPage(TimelapsePreviewViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopTimer();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnTogglePlayClicked(object? sender, EventArgs e)
        => _vm.TogglePlayCommand.Execute(null);

    private void OnStopClicked(object? sender, EventArgs e)
        => _vm.StopCommand.Execute(null);

    private async void OnExportClicked(object? sender, EventArgs e)
        => await _vm.ExportCommand.ExecuteAsync(null);
}
