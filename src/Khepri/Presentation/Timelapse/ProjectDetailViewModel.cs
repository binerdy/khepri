using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

[QueryProperty(nameof(ProjectId), "projectId")]
public sealed partial class ProjectDetailViewModel(TimelapseService timelapseService) : ObservableObject
{
    private Guid _projectId;

    // Shell passes the query parameter as a string.
    public string ProjectId
    {
        set
        {
            _projectId = Guid.Parse(value);
            _ = LoadCommand.ExecuteAsync(null);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FramesDescending))]
    [NotifyPropertyChangedFor(nameof(HasFrames))]
    public partial TimelapseProject? Project { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool HasFrames => Project?.Frames.Count > 0;

    public IEnumerable<TimelapseFrame> FramesDescending =>
        Project?.Frames.Reverse() ?? [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var all = await timelapseService.GetAllProjectsAsync(cancellationToken);
            Project = all.FirstOrDefault(p => p.Id == _projectId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CaptureAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await timelapseService.CaptureFrameAsync(_projectId, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // user dismissed the camera picker — not an error
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Capture failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RetakeAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await timelapseService.RetakeLastFrameAsync(_projectId, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // user dismissed the camera picker — not an error
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Retake failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
