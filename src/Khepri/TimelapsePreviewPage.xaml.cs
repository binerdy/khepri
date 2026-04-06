// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class TimelapsePreviewPage : ContentPage
{
    private readonly TimelapsePreviewViewModel _vm;

    // Tracks what is currently rendered so the dissolve overlay can show the outgoing frame.
    private string? _displayedPath;
    private double  _displayedOffsetX;
    private double  _displayedOffsetY;
    private double  _displayedRotation;
    private double  _displayedScale = 1d;

    public TimelapsePreviewPage(TimelapsePreviewViewModel vm)
    {
        _vm = vm;
        // Subscribe BEFORE BindingContext so our handler fires first — this lets us
        // capture FrameImage.Source (old frame) before MAUI bindings update it.
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        InitializeComponent();
        BindingContext = vm;
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
        // Track current display state from secondary notifications so we always
        // have the outgoing frame info ready for the dissolve overlay.
        if (e.PropertyName == nameof(TimelapsePreviewViewModel.CurrentFramePath))
        {
            _displayedPath = _vm.CurrentFramePath;
            return;
        }
        if (e.PropertyName == nameof(TimelapsePreviewViewModel.CurrentOffsetX))
        {
            _displayedOffsetX = _vm.CurrentOffsetX;
            return;
        }
        if (e.PropertyName == nameof(TimelapsePreviewViewModel.CurrentOffsetY))
        {
            _displayedOffsetY = _vm.CurrentOffsetY;
            return;
        }
        if (e.PropertyName == nameof(TimelapsePreviewViewModel.CurrentRotation))
        {
            _displayedRotation = _vm.CurrentRotation;
            return;
        }
        if (e.PropertyName == nameof(TimelapsePreviewViewModel.CurrentScale))
        {
            _displayedScale = _vm.CurrentScale;
            return;
        }

        if (e.PropertyName != nameof(TimelapsePreviewViewModel.CurrentFrameIndex))
        {
            return;
        }

        // At this point our handler fires BEFORE MAUI bindings update FrameImage — so
        // _displayedPath / _displayedOffsetX/Y still hold the OLD frame's values.
        switch (_vm.TransitionIndex)
        {
            case 0: // Dissolve — old frame fades out while new frame fades in beneath it
            {
                var oldPath    = _displayedPath;
                var oldOffsetX = _displayedOffsetX;
                var oldOffsetY = _displayedOffsetY;
                var oldRotation = _displayedRotation;
                var oldScale    = _displayedScale;

                if (oldPath != null && oldPath != _vm.CurrentFramePath)
                {
                    FrameImageOverlay.Source       = ImageSource.FromFile(oldPath);
                    FrameImageOverlay.TranslationX = oldOffsetX;
                    FrameImageOverlay.TranslationY = oldOffsetY;
                    FrameImageOverlay.Rotation     = oldRotation;
                    FrameImageOverlay.Scale        = oldScale;
                    FrameImageOverlay.Opacity      = 1;
                    FrameImageOverlay.IsVisible    = true;
                    FrameImage.Opacity             = 0;

                    // Yield so MAUI bindings update FrameImage to the new frame.
                    await Task.Yield();

                    await Task.WhenAll(
                        FrameImageOverlay.FadeToAsync(0, 350),
                        FrameImage.FadeToAsync(1, 350));

                    FrameImageOverlay.IsVisible = false;
                }
                else
                {
                    FrameImage.Opacity = 1;
                }
                break;
            }

            case 1: // None
                FrameImage.Opacity = 1;
                break;

            case 2: // Fade-in — new frame materialises from black
                FrameImage.Opacity = 0;
                await FrameImage.FadeToAsync(1, 250, Easing.CubicIn);
                break;

            case 3: // Flip — quick page-riffle snap
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
