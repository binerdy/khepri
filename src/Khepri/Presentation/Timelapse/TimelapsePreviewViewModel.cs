// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

[QueryProperty(nameof(RawProjectId), "projectId")]
public sealed partial class TimelapsePreviewViewModel(
    TimelapseService timelapseService,
    AlignmentService alignmentService,
    IVideoExportService videoExportService) : ObservableObject
{
    private Guid _projectId;
    private IReadOnlyList<string>? _framePaths;
    private IDispatcherTimer? _timer;
    private int _currentIndex;

    public string RawProjectId
    {
        set
        {
            _projectId = Guid.Parse(value);
            _ = LoadAsync();
        }
    }

    [ObservableProperty]
    public partial string? ProjectName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(CurrentFramePath))]
    public partial int CurrentFrameIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int FrameCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotPlaying))]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    public partial bool IsPlaying { get; set; }

    public string PlayPauseText => IsPlaying ? "⏸ PAUSE" : "▶ PLAY";

    [ObservableProperty]
    public partial double SecondsPerFrame { get; set; } = 0.5;

    // 0 = None, 1 = Fade
    [ObservableProperty]
    public partial int TransitionIndex { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotExporting))]
    public partial bool IsExporting { get; set; }

    [ObservableProperty]
    public partial double ExportProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlignButtonVisible))]
    public partial bool IsClone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlignButtonVisible))]
    public partial bool IsAligning { get; set; }

    [ObservableProperty]
    public partial double AlignProgress { get; set; }

    [ObservableProperty]
    public partial int AlgorithmIndex { get; set; } = 0;

    public string[] AlgorithmNames { get; } =
    [
        "Facial Landmark Warp",
        "Phase Correlation",
        "ORB Feature Matching",
        "Laplacian Crop",
    ];

    public bool IsAlignButtonVisible => IsClone && !IsAligning;

    public bool IsNotPlaying   => !IsPlaying;
    public bool IsNotExporting => !IsExporting;

    public string? CurrentFramePath
        => (_framePaths is { Count: > 0 })
           ? _framePaths[Math.Clamp(_currentIndex, 0, _framePaths.Count - 1)]
           : null;

    public string ProgressText => FrameCount > 0 ? $"{CurrentFrameIndex + 1} / {FrameCount}" : "—";

    public string[] TransitionNames { get; } = ["None", "Fade"];

    private async Task LoadAsync()
    {
        var all = await timelapseService.GetAllProjectsAsync();
        var project = all.FirstOrDefault(p => p.Id == _projectId);
        if (project is null)
        {
            return;
        }

        ProjectName = project.Name.ToUpperInvariant();
        IsClone = project.IsClone;
        _framePaths = project.Frames
            .OrderBy(f => f.Index)
            .Select(f => f.ActiveFilePath)
            .ToList();

        FrameCount = _framePaths.Count;
        CurrentFrameIndex = 0;
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            _timer?.Stop();
            IsPlaying = false;
        }
        else
        {
            if (_framePaths is null or { Count: 0 })
            {
                return;
            }

            _timer = Microsoft.Maui.Controls.Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(SecondsPerFrame);
            _timer.Tick += OnTimerTick;
            _timer.Start();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _timer?.Stop();
        IsPlaying = false;
        _currentIndex = 0;
        CurrentFrameIndex = 0;
    }

    public void StopTimer()
    {
        _timer?.Stop();
        IsPlaying = false;
    }

    partial void OnSecondsPerFrameChanged(double value)
    {
        if (_timer is not null && _timer.IsRunning)
        {
            _timer.Interval = TimeSpan.FromSeconds(value);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_framePaths is null or { Count: 0 })
        {
            return;
        }

        _currentIndex = (_currentIndex + 1) % _framePaths.Count;
        CurrentFrameIndex = _currentIndex;
    }

    [RelayCommand]
    private async Task AlignAsync(CancellationToken cancellationToken)
    {
        var algorithm = (AlignmentAlgorithm)AlgorithmIndex;
        if (algorithm != AlignmentAlgorithm.FacialLandmarkWarp)
        {
            await Shell.Current.DisplayAlertAsync(
                "Not Yet Available",
                $"'{AlgorithmNames[AlgorithmIndex]}' is not yet implemented.",
                "OK");
            return;
        }

        StopTimer();
        IsAligning = true;
        AlignProgress = 0;

        try
        {
            var progressReporter = new Progress<(int Current, int Total)>(r =>
                AlignProgress = r.Total > 0 ? (double)r.Current / r.Total : 0);

            await alignmentService.AlignProjectAsync(_projectId, progressReporter, cancellationToken);

            // Reload frame paths — ActiveFilePath now points to the aligned images.
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // dismissed
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Alignment failed", ex.Message, "OK");
        }
        finally
        {
            IsAligning = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (_framePaths is null or { Count: 0 })
        {
            return;
        }

        StopTimer();
        IsExporting = true;
        ExportProgress = 0;

        try
        {
            var transition = TransitionIndex == 1 ? TransitionEffect.Fade : TransitionEffect.None;
            var progressReporter = new Progress<int>(pct => ExportProgress = pct / 100.0);

            var path = await videoExportService.ExportAsync(
                _framePaths,
                SecondsPerFrame,
                transition,
                progressReporter,
                cancellationToken);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"{ProjectName ?? "Timelapse"}.mp4",
                File  = new ShareFile(path, "video/mp4"),
            });
        }
        catch (OperationCanceledException)
        {
            // dismissed
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Export failed", ex.Message, "OK");
        }
        finally
        {
            IsExporting = false;
        }
    }
}
