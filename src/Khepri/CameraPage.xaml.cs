// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Maui.Core;
using Khepri.Infrastructure.Timelapse;
#if ANDROID
using Android.Graphics;
using Android.Media;
using Path = System.IO.Path;
#endif

namespace Khepri;

public partial class CameraPage : ContentPage
{
    public string? OverlayImagePath { get; set; }
    public string? FramesDir { get; set; }

    private bool _capturing;
    private bool _cameraStarted;
    private bool _flipping;
    private bool _isFrontCamera;

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
            _isFrontCamera = _cameras[_activeCameraIndex].Position == CameraPosition.Front;
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
            _isFrontCamera = _cameras[_activeCameraIndex].Position == CameraPosition.Front;

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
        // Buffer the stream immediately before any async suspension.
        // e.Media is already in-memory (MemoryStream from the camera layer), so this
        // is a fast buffer copy that keeps the byte array alive across thread boundaries.
        if (e.Media.CanSeek)
        {
            e.Media.Seek(0, SeekOrigin.Begin);
        }
        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            e.Media.CopyTo(ms);
            imageBytes = ms.ToArray();
        }

        // Offload all disk I/O and bitmap processing to the thread pool.
        // MediaCaptured fires on the main thread; FlipImageHorizontally in particular is
        // expensive (BitmapFactory.DecodeFile + Matrix + Compress) and would freeze the UI.
        // Task.Run is the .NET equivalent of withContext(Dispatchers.IO) in Kotlin.
        string? captured = null;
        try
        {
            captured = await Task.Run(async () =>
            {
                var destDir = FramesDir ?? Path.Combine(FileSystem.AppDataDirectory, "frames");
                Directory.CreateDirectory(destDir);
                var destPath = Path.Combine(destDir, $"{Guid.NewGuid()}.jpg");

                await File.WriteAllBytesAsync(destPath, imageBytes);

                // Front camera output is horizontally mirrored relative to the preview.
                // Flip it back so it stays aligned with the overlay.
#if ANDROID
                if (_isFrontCamera)
                {
                    FlipImageHorizontally(destPath);
                }
#endif
                return destPath;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Khepri] Frame save failed: {ex.Message}");
        }

        // StopCameraPreview → ProcessCameraProvider.UnbindAll() AND PopModalAsync →
        // CameraViewHandler.DisconnectHandler → UnbindAll() both assert the main thread.
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

#if ANDROID
    private static void FlipImageHorizontally(string path)
    {
        var bmp = BitmapFactory.DecodeFile(path);
        if (bmp is null)
        {
            return;
        }

        try
        {
            // Normalise EXIF rotation first, then apply horizontal flip.
            var exif = new ExifInterface(path);
            var orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, 1);
            float rotateDegrees = orientation switch
            {
                6 => 90f,
                3 => 180f,
                8 => 270f,
                _ => 0f
            };

            var matrix = new Matrix();
            if (rotateDegrees != 0f)
            {
                matrix.PostRotate(rotateDegrees);
            }

            matrix.PostScale(-1f, 1f);              // horizontal flip

            var flipped = Bitmap.CreateBitmap(bmp, 0, 0, bmp.Width, bmp.Height, matrix, true)!;
            bmp.Recycle();

            using var stream = File.Create(path);
            flipped.Compress(Bitmap.CompressFormat.Jpeg!, 92, stream);
            flipped.Recycle();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Khepri] Front-camera flip failed: {ex.Message}");
        }
    }
#endif
}
