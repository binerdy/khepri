using Khepri.Domain.Timelapse;

namespace Khepri.Infrastructure.Timelapse;

/// <summary>
/// Launches CameraPage modally so the user sees a live viewfinder with
/// the previous frame ghosted as an overlay. The captured file is saved
/// directly into the projects folder by CameraPage, then the path is
/// handed back here via the static result slot.
/// </summary>
public sealed class MauiCameraService : ICameraService
{
    private static TaskCompletionSource<string?>? _tcs;

    /// <summary>Called by CameraPage when a photo is taken or cancelled.</summary>
    public static void SetResult(string? filePath)
        => Interlocked.Exchange(ref _tcs, null)?.TrySetResult(filePath);

    public async Task<string> CapturePhotoAsync(string? overlayImagePath = null, CancellationToken cancellationToken = default)
    {
        _tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = cancellationToken.Register(() =>
        {
            SetResult(null);
        });

        var page = new Khepri.CameraPage { OverlayImagePath = overlayImagePath };
        await Shell.Current.Navigation.PushModalAsync(page, animated: false);

        var filePath = await _tcs.Task;

        if (filePath is null)
            throw new OperationCanceledException("Photo capture was cancelled.");

        return filePath;
    }
}
