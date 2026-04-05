namespace Khepri.Domain.Timelapse;

/// <summary>
/// Abstracts the device camera. Implemented in Infrastructure (platform-specific).
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Captures a photo and returns the local file path of the saved image.
    /// When <paramref name="overlayImagePath"/> is provided, the live viewfinder
    /// shows that image as a semi-transparent ghost to help frame the shot.
    /// </summary>
    Task<string> CapturePhotoAsync(string? overlayImagePath = null, CancellationToken cancellationToken = default);
}
