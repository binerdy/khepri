// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;

namespace Khepri.Presentation.Timelapse;

public sealed partial class TimelapsePreviewViewModel(
    TimelapseService timelapseService,
    IVideoExportService videoExportService) : ObservableObject, IQueryAttributable
{
    private Guid _projectId;
    private IReadOnlyList<string>? _framePaths;
    private IReadOnlyList<(double X, double Y)>? _frameOffsets;
    private IReadOnlyList<double>? _frameRotations;
    private IReadOnlyList<double>? _frameScales;
    private IDispatcherTimer? _timer;
    private int _currentIndex;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("projectId", out var value))
        {
            _projectId = Guid.Parse(value?.ToString() ?? string.Empty);
            _ = LoadAsync();
        }
    }

    [ObservableProperty]
    public partial string? ProjectName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(CurrentFramePath))]
    [NotifyPropertyChangedFor(nameof(CurrentOffsetX))]
    [NotifyPropertyChangedFor(nameof(CurrentOffsetY))]
    [NotifyPropertyChangedFor(nameof(CurrentRotation))]
    [NotifyPropertyChangedFor(nameof(CurrentScale))]
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

    // 0 = Dissolve, 1 = None, 2 = Fade, 3 = Flip
    [ObservableProperty]
    public partial int TransitionIndex { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotExporting))]
    public partial bool IsExporting { get; set; }

    [ObservableProperty]
    public partial double ExportProgress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SettingsToggleText))]
    public partial bool ShowSettings { get; set; } = false;

    public string SettingsToggleText => ShowSettings ? "▲ SETTINGS" : "▼ SETTINGS";

    /// <summary>Transition duration in seconds (0.05 – 1.0). Does not apply to Transition = None.</summary>
    [ObservableProperty]
    public partial double TransitionDuration { get; set; } = 0.35;

    public bool IsNotPlaying => !IsPlaying;
    public bool IsNotExporting => !IsExporting;

    public string? CurrentFramePath
        => (_framePaths is { Count: > 0 })
           ? _framePaths[Math.Clamp(_currentIndex, 0, _framePaths.Count - 1)]
           : null;

    public double CurrentOffsetX
        => (_frameOffsets is { Count: > 0 })
           ? _frameOffsets[Math.Clamp(_currentIndex, 0, _frameOffsets.Count - 1)].X
           : 0;

    public double CurrentOffsetY
        => (_frameOffsets is { Count: > 0 })
           ? _frameOffsets[Math.Clamp(_currentIndex, 0, _frameOffsets.Count - 1)].Y
           : 0;

    public double CurrentRotation
        => (_frameRotations is { Count: > 0 })
           ? _frameRotations[Math.Clamp(_currentIndex, 0, _frameRotations.Count - 1)]
           : 0;

    public double CurrentScale
        => (_frameScales is { Count: > 0 })
           ? _frameScales[Math.Clamp(_currentIndex, 0, _frameScales.Count - 1)]
           : 1d;

    public string ProgressText => FrameCount > 0 ? $"{CurrentFrameIndex + 1} / {FrameCount}" : "—";

    public string[] TransitionNames { get; } = ["Dissolve", "None", "Fade", "Flip"];

    // Called from OnNavigatedTo so the page always shows up-to-date offsets,
    // even when it is returned to via the navigation back-stack.
    public Task RefreshAsync() => LoadAsync();

    /// <summary>Advances one frame; works whether playback is running or paused.</summary>
    public void AdvanceOneFrame()
    {
        if (_framePaths is null or { Count: 0 })
        {
            return;
        }

        _currentIndex = (_currentIndex + 1) % _framePaths.Count;
        CurrentFrameIndex = _currentIndex;
    }

    private async Task LoadAsync()
    {
        var all = await timelapseService.GetAllProjectsAsync();
        var project = all.FirstOrDefault(p => p.Id == _projectId);
        if (project is null)
        {
            return;
        }

        ProjectName = project.Name.ToUpperInvariant();
        var ordered = project.Frames.OrderBy(f => f.Index).ToList();
        _framePaths = ordered.Select(f => f.ActiveFilePath).ToList();
        _frameOffsets = ordered.Select(f => (f.OffsetX, f.OffsetY)).ToList();
        _frameRotations = ordered.Select(f => f.Rotation).ToList();
        _frameScales = ordered.Select(f => f.Scale).ToList();
        _currentIndex = 0;
        FrameCount = _framePaths.Count;
        CurrentFrameIndex = 0;
        // [ObservableProperty] skips notification when the value is unchanged (0→0).
        // Manually notify so the image and offset bindings always reflect the
        // freshly-loaded data.
        OnPropertyChanged(nameof(CurrentFramePath));
        OnPropertyChanged(nameof(CurrentOffsetX));
        OnPropertyChanged(nameof(CurrentOffsetY));
        OnPropertyChanged(nameof(CurrentRotation));
        OnPropertyChanged(nameof(CurrentScale));
    }

    [RelayCommand]
    private void ToggleSettings() => ShowSettings = !ShowSettings;

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
            var transition = TransitionIndex == 2 ? TransitionEffect.Fade : TransitionEffect.None;
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
                File = new ShareFile(path, "video/mp4"),
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
