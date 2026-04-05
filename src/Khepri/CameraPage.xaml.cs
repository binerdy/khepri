using CommunityToolkit.Maui.Core;
using Khepri.Infrastructure.Timelapse;

namespace Khepri;

public partial class CameraPage : ContentPage
{
    public string? OverlayImagePath { get; set; }

    private bool _capturing;
    private bool _cameraStarted;

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
            return;

        await CameraPreview.StartCameraPreview(CancellationToken.None);
        _cameraStarted = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        CameraPreview.MediaCaptured      += OnMediaCaptured;
        CameraPreview.MediaCaptureFailed += OnMediaCaptureFailed;

        if (!string.IsNullOrEmpty(OverlayImagePath) && File.Exists(OverlayImagePath))
        {
            OverlayImage.Source     = ImageSource.FromFile(OverlayImagePath);
            OverlayImage.Opacity    = OpacitySlider.Value;
            OverlayImage.IsVisible  = true;
            OpacitySlider.IsVisible = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraPreview.MediaCaptured      -= OnMediaCaptured;
        CameraPreview.MediaCaptureFailed -= OnMediaCaptureFailed;

        // Safety-net: only stop if the capture/cancel paths haven't already done so.
        // MainThread dispatch is required — ProcessCameraProvider.UnbindAll() asserts main thread.
        if (_cameraStarted)
        {
            _cameraStarted = false;
            _ = MainThread.InvokeOnMainThreadAsync(() => CameraPreview.StopCameraPreview());
        }
    }

    private void OnOpacityChanged(object? sender, ValueChangedEventArgs e)
        => OverlayImage.Opacity = e.NewValue;

    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        if (_capturing) return;
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
                e.Media.Seek(0, SeekOrigin.Begin);

            var destDir = Path.Combine(FileSystem.AppDataDirectory, "frames");
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
            await MainThread.InvokeOnMainThreadAsync(() => CameraPreview.StopCameraPreview());
        }

        await Navigation.PopModalAsync(animated: false);
        MauiCameraService.SetResult(null);
    }
}
