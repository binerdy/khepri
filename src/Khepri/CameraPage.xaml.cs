// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Core;
using Khepri.Infrastructure.Timelapse;

namespace Khepri;

public partial class CameraPage : ContentPage
{
    public string? OverlayImagePath { get; set; }
    public string? FramesDir { get; set; }

    private bool _capturing;
    private bool _cameraStarted;
    private bool _flipping;

    // Available cameras discovered on first load.
    private IReadOnlyList<CameraInfo>? _cameras;
    // Index of the currently active camera in _cameras.
    private int _activeCameraIndex;

    public CameraPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            return;
        }

        // Discover available cameras and pre-select the rear camera (index 0 fallback).
        _cameras = await CameraPreview.GetAvailableCameras(CancellationToken.None);
        if (_cameras.Count > 0)
        {
            // Prefer rear camera as the default; fall back to index 0.
            var rearIdx = -1;
            for (var i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i].Position == CameraPosition.Rear) { rearIdx = i; break; }
            }
            _activeCameraIndex = rearIdx >= 0 ? rearIdx : 0;
            CameraPreview.SelectedCamera = _cameras[_activeCameraIndex];
        }

        await CameraPreview.StartCameraPreview(CancellationToken.None);
        _cameraStarted = true;

        // Show the flip button only when at least one front and one rear exist.
        FlipButton.IsVisible = _cameras.Any(c => c.Position == CameraPosition.Front)
                            && _cameras.Any(c => c.Position == CameraPosition.Rear);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        CameraPreview.MediaCaptured += OnMediaCaptured;
        CameraPreview.MediaCaptureFailed += OnMediaCaptureFailed;

        if (!string.IsNullOrEmpty(OverlayImagePath) && File.Exists(OverlayImagePath))
        {
            OverlayImage.Source = ImageSource.FromFile(OverlayImagePath);
            OverlayImage.Opacity = OpacitySlider.Value;
            OverlayImage.IsVisible = true;
            OpacitySlider.IsVisible = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraPreview.MediaCaptured -= OnMediaCaptured;
        CameraPreview.MediaCaptureFailed -= OnMediaCaptureFailed;

        // Safety-net: only stop if the capture/cancel paths haven't already done so.
        // MainThread dispatch is required — ProcessCameraProvider.UnbindAll() asserts main thread.
        if (_cameraStarted)
        {
            _cameraStarted = false;
            _ = MainThread.InvokeOnMainThreadAsync(CameraPreview.StopCameraPreview);
        }
    }

    private void OnOpacityChanged(object? sender, ValueChangedEventArgs e)
        => OverlayImage.Opacity = e.NewValue;

    private async void OnFlipClicked(object? sender, EventArgs e)
    {
        if (_flipping || !_cameraStarted || _cameras is null or { Count: < 2 })
        {
            return;
        }

        _flipping = true;
        try
        {
            // Find the first camera with a different position than the current one.
            var currentPosition = _cameras[_activeCameraIndex].Position;
            var nextIndex = -1;
            for (var i = 0; i < _cameras.Count; i++)
            {
                if (i != _activeCameraIndex && _cameras[i].Position != currentPosition)
                {
                    nextIndex = i;
                    break;
                }
            }
            // Fallback: just cycle to the next camera index.
            if (nextIndex < 0)
            {
                nextIndex = (_activeCameraIndex + 1) % _cameras.Count;
            }

            _cameraStarted = false;
            CameraPreview.StopCameraPreview();

            _activeCameraIndex = nextIndex;
            CameraPreview.SelectedCamera = _cameras[_activeCameraIndex];

            await CameraPreview.StartCameraPreview(CancellationToken.None);
            _cameraStarted = true;
        }
        finally
        {
            _flipping = false;
        }
    }

    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        if (_capturing)
        {
            return;
        }

        _capturing = true;
        await CameraPreview.CaptureImage(CancellationToken.None);
        // Result delivered via MediaCaptured event.
    }

    private async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        // Save the image on the thread-pool thread where this event fires.
        string? destPath = null;
        try
        {
            if (e.Media.CanSeek)
            {
                e.Media.Seek(0, SeekOrigin.Begin);
            }

            var destDir = FramesDir ?? Path.Combine(FileSystem.AppDataDirectory, "frames");
            Directory.CreateDirectory(destDir);
            destPath = Path.Combine(destDir, $"{Guid.NewGuid()}.jpg");

            await using var dst = File.OpenWrite(destPath);
            await e.Media.CopyToAsync(dst);
        }
        catch (Exception ex)
        {
            destPath = null;
            System.Diagnostics.Debug.WriteLine($"[Khepri] Frame save failed: {ex.Message}");
        }

        // StopCameraPreview → ProcessCameraProvider.UnbindAll() AND PopModalAsync →
        // CameraViewHandler.DisconnectHandler → UnbindAll() both assert the main thread.
        // Capture the result path in a local before switching threads.
        var captured = destPath;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_cameraStarted)
            {
                _cameraStarted = false;
                CameraPreview.StopCameraPreview();
            }
            await Navigation.PopModalAsync(animated: false);
            MauiCameraService.SetResult(captured);
        });
    }

    private async void OnMediaCaptureFailed(object? sender, MediaCaptureFailedEventArgs e)
    {
        _capturing = false;
        await DisplayAlertAsync("Capture failed", e.FailureReason, "OK");
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_cameraStarted)
        {
            _cameraStarted = false;
            await MainThread.InvokeOnMainThreadAsync(CameraPreview.StopCameraPreview);
        }

        await Navigation.PopModalAsync(animated: false);
        MauiCameraService.SetResult(null);
    }
}
