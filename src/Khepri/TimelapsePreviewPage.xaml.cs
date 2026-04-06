// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
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
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopTimer();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        _ = _vm.RefreshAsync();
    }

    // ── Tap to advance ────────────────────────────────────────────────────────

    private void OnFrameTapped(object? sender, TappedEventArgs e)
        => _vm.AdvanceOneFrame();

    // ── Transition animations ─────────────────────────────────────────────────

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimelapsePreviewViewModel.CurrentFrameIndex))
        {
            return;
        }

        switch (_vm.TransitionIndex)
        {
            case 1: // Fade — new frame fades in
                FrameImage.Opacity = 0;
                await FrameImage.FadeToAsync(1, 250, Easing.CubicIn);
                break;

            case 2: // Flip — quick scale snap like riffling a page
                await Task.WhenAll(
                    FrameImage.ScaleToAsync(0.96, 50, Easing.CubicIn),
                    FrameImage.FadeToAsync(0.5, 50));
                await Task.WhenAll(
                    FrameImage.ScaleToAsync(1.0, 50, Easing.CubicOut),
                    FrameImage.FadeToAsync(1.0, 50));
                break;
        }
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
