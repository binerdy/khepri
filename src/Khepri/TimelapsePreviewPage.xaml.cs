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
    private double _displayedOffsetX;
    private double _displayedOffsetY;
    private double _displayedRotation;
    private double _displayedScale = 1d;

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
                    var oldPath = _displayedPath;
                    var oldOffsetX = _displayedOffsetX;
                    var oldOffsetY = _displayedOffsetY;
                    var oldRotation = _displayedRotation;
                    var oldScale = _displayedScale;

                    if (oldPath != null && oldPath != _vm.CurrentFramePath)
                    {
                        FrameImageOverlay.Source = ImageSource.FromFile(oldPath);
                        FrameImageOverlay.TranslationX = oldOffsetX;
                        FrameImageOverlay.TranslationY = oldOffsetY;
                        FrameImageOverlay.Rotation = oldRotation;
                        FrameImageOverlay.Scale = oldScale;
                        FrameImageOverlay.Opacity = 1;
                        FrameImageOverlay.IsVisible = true;
                        FrameImage.Opacity = 0;

                        // Yield so MAUI bindings update FrameImage to the new frame.
                        await Task.Yield();

                        var dissolveDuration = (uint)(_vm.TransitionDuration * 1000);
                        await Task.WhenAll(
                            FrameImageOverlay.FadeToAsync(0, dissolveDuration),
                            FrameImage.FadeToAsync(1, dissolveDuration));

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
                await FrameImage.FadeToAsync(1, (uint)(_vm.TransitionDuration * 1000), Easing.CubicIn);
                break;

            case 3: // Flip — page turns away revealing the next frame beneath
                {
                    var oldPathF = _displayedPath;
                    var oldOffsetXF = _displayedOffsetX;
                    var oldOffsetYF = _displayedOffsetY;
                    var oldRotationF = _displayedRotation;
                    var oldScaleF = _displayedScale;

                    var half = (uint)Math.Max(30, _vm.TransitionDuration * 500);

                    if (oldPathF != null && oldPathF != _vm.CurrentFramePath)
                    {
                        // Pin old frame on top; new frame sits underneath (updated by binding after Yield).
                        FrameImageOverlay.Source = ImageSource.FromFile(oldPathF);
                        FrameImageOverlay.TranslationX = oldOffsetXF;
                        FrameImageOverlay.TranslationY = oldOffsetYF;
                        FrameImageOverlay.Rotation = oldRotationF;
                        FrameImageOverlay.Scale = oldScaleF;
                        FrameImageOverlay.RotationY = 0;
                        FrameImageOverlay.Opacity = 1;
                        FrameImageOverlay.IsVisible = true;

                        // New frame starts edge-on (invisible) until the old page has gone.
                        FrameImage.RotationY = -90;
                        FrameImage.Opacity = 1;

                        await Task.Yield(); // let bindings update FrameImage to the new path

                        // Phase 1: old page rotates away (0° → 90° = edge-on = invisible).
                        await FrameImageOverlay.RotateYToAsync(90, half, Easing.CubicIn);
                        FrameImageOverlay.IsVisible = false;
                        FrameImageOverlay.RotationY = 0; // reset for next use

                        // Phase 2: new page swings in (-90° → 0° = facing viewer).
                        await FrameImage.RotateYToAsync(0, half, Easing.CubicOut);
                    }
                    else
                    {
                        FrameImage.RotationY = 0;
                        FrameImage.Opacity = 1;
                    }
                    break;
                }
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
