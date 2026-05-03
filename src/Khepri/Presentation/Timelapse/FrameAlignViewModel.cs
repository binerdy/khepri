// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

/// <summary>A thumbnail entry in the alignment filmstrip.</summary>
public sealed record FilmstripItem(string FilePath, bool IsActive)
{
    // Avoid DataTrigger (never compiled by MAUI source gen) — drive StrokeThickness
    // from a compiled binding; Stroke color is set statically via AppThemeBinding in XAML.
    public double ActiveStrokeThickness => IsActive ? 2.0 : 0.0;
}

/// <summary>
/// Drives the ghost-frame manual alignment page.
///
/// Frame[0] is the fixed reference (never moved).  For each step (index 0 … N-2):
///   • <see cref="GhostPath"/>       = Frame[CurrentIndex]   — already-aligned reference, shown at its
///                                     saved playback offset so it represents exactly where it will
///                                     appear in the timelapse.
///   • <see cref="BackgroundPath"/>  = Frame[CurrentIndex+1] — the frame being repositioned (opaque,
///                                     draggable).  Its saved offset is pre-loaded so the user starts
///                                     from the last saved state.
///
/// The user drags the background until its subject overlaps the ghost, then:
///   PREV  — navigate back (no save).
///   SAVE  — persist the current offset without navigating.
///   NEXT  — navigate forward (no save).
/// </summary>
public sealed partial class FrameAlignViewModel(
    TimelapseService timelapseService,
    FrameAlignService alignService) : ObservableObject, IQueryAttributable
{
    private Guid _projectId;
    private List<TimelapseFrame>? _frames;

    // ─── Query property ───────────────────────────────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("projectId", out var value))
        {
            _projectId = Guid.Parse(value?.ToString() ?? string.Empty);
            _ = LoadAsync();
        }
    }

    // ─── Observable state ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundPath))]
    [NotifyPropertyChangedFor(nameof(GhostPath))]
    [NotifyPropertyChangedFor(nameof(GhostOffsetX))]
    [NotifyPropertyChangedFor(nameof(GhostOffsetY))]
    [NotifyPropertyChangedFor(nameof(GhostRotation))]
    [NotifyPropertyChangedFor(nameof(GhostScale))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(FilmstripItems))]
    [NotifyPropertyChangedFor(nameof(CanGoPrev))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial int CurrentIndex { get; private set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(FilmstripItems))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial int FrameCount { get; private set; }

    [ObservableProperty] public partial double OffsetX { get; set; }
    [ObservableProperty] public partial double OffsetY { get; set; }
    [ObservableProperty] public partial double Rotation { get; set; }
    [ObservableProperty] public partial double Scale { get; set; } = 1d;

    /// <summary>
    /// dp width of the alignment viewer at the time the current gesture started.
    /// Set by the platform touch listener (or precision panel) before triggering a save.
    /// </summary>
    public double ReferenceViewWidth { get; set; }

    /// <summary>Opacity for the ghost (next-frame) overlay. Range 0.1 – 0.9.</summary>
    [ObservableProperty] public partial double GhostOpacity { get; set; } = 0.4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; private set; }

    // ─── Derived properties ───────────────────────────────────────────────────

    /// <summary>Bottom layer — the frame being repositioned (opaque, draggable).</summary>
    public string? BackgroundPath => (_frames is { Count: > 0 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count)
        ? _frames[Math.Min(CurrentIndex + 1, _frames.Count - 1)].ActiveFilePath
        : null;

    /// <summary>Top layer — the already-aligned reference frame (transparent, fixed).</summary>
    public string? GhostPath => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].ActiveFilePath
        : null;

    /// <summary>Playback offset of the ghost (reference) frame so it renders at its saved position.</summary>
    public double GhostOffsetX => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].OffsetX
        : 0;

    /// <inheritdoc cref="GhostOffsetX"/>
    public double GhostOffsetY => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].OffsetY
        : 0;

    public double GhostRotation => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].Rotation
        : 0;

    public double GhostScale => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].Scale
        : 1d;

    /// <summary>e.g. "FRAME 2 / 6" — shows which frame number is being repositioned (1-based).</summary>
    public string ProgressText => FrameCount > 1
        ? $"FRAME {Math.Min(CurrentIndex + 2, FrameCount)} / {FrameCount}"
        : "—";

    public bool CanGoPrev => CurrentIndex > 0;
    public bool CanGoNext => CurrentIndex < FrameCount - 1;
    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Small thumbnails shown below the viewer. The frame currently being repositioned
    /// (frame[CurrentIndex+1], or frame[N-1] at the preview-only last step) is highlighted.
    /// </summary>
    public IReadOnlyList<FilmstripItem> FilmstripItems
    {
        get
        {
            if (_frames is null || CurrentIndex < 0)
            {
                return [];
            }

            var activeIdx = Math.Min(CurrentIndex + 1, _frames.Count - 1);
            return _frames
                .Select((f, i) => new FilmstripItem(f.ActiveFilePath, i == activeIdx))
                .ToList();
        }
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        var all = await timelapseService.GetAllProjectsAsync();
        var project = all.FirstOrDefault(p => p.Id == _projectId);
        if (project is null)
        {
            return;
        }

        _frames = project.Frames.OrderBy(f => f.Index).ToList();
        FrameCount = _frames.Count;
        SetIndex(0);
    }

    // ─── Auto-save ────────────────────────────────────────────────────────────

    /// <summary>Fired on the calling thread after a successful debounced save.</summary>
    public event EventHandler? SaveCompleted;

    private CancellationTokenSource? _saveCts;

    /// <summary>
    /// Debounced auto-save.  Multiple rapid calls within 400 ms coalesce into one write.
    /// Intended to be fire-and-forget from gesture recognisers.
    /// </summary>
    public async Task AutoSaveAsync()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _saveCts = cts;

        try
        {
            await Task.Delay(400, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer gesture
        }

        await ExecuteSaveAsync(cts.Token);
    }

    private async Task ExecuteSaveAsync(CancellationToken cancellationToken)
    {
        if (_frames is null || CurrentIndex >= _frames.Count - 1)
        {
            return; // preview-only last step has nothing to save
        }

        IsBusy = true;
        try
        {
            var frame = _frames[CurrentIndex + 1];
            await alignService.SaveAlignmentAsync(
                _projectId, frame.Id, OffsetX, OffsetY, Rotation, Scale, ReferenceViewWidth, cancellationToken);
            // Keep in-memory list in sync so ghost values reflect saved transforms.
            frame.SetTransform(OffsetX, OffsetY, Rotation, Scale, ReferenceViewWidth);
            SaveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Explicit declaration so the MAUI XAML source generator can produce a compiled
    // (trim-safe) binding — source-gen-produced [RelayCommand] properties are
    // invisible to the parallel MAUI source generator at build time.
    private AsyncRelayCommand? _resetAllCommand;
    public IAsyncRelayCommand ResetAllCommand => _resetAllCommand ??= new AsyncRelayCommand(ResetAllAsync);

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Resets the current frame's transform back to identity in memory (not persisted).</summary>
    [RelayCommand]
    private void Reset()
    {
        OffsetX = 0;
        OffsetY = 0;
        Rotation = 0;
        Scale = 1;
    }

    private async Task ResetAllAsync(CancellationToken cancellationToken)
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Reset alignment",
            "Clear all saved positions and return every frame to its original placement?",
            "RESET",
            "CANCEL");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await alignService.ResetAllAlignmentAsync(_projectId, cancellationToken);
            await LoadAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Reset failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Prev()
    {
        if (CanGoPrev)
        {
            SetIndex(CurrentIndex - 1);
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (CanGoNext)
        {
            SetIndex(CurrentIndex + 1);
        }
    }

    private void SetIndex(int index)
    {
        if (_frames is not null && index < _frames.Count)
        {
            // Load the saved transform of the frame that will be displayed as the background.
            // At normal alignment steps: background is frame[index + 1].
            // At the last preview-only step: background is frame[index] (the final frame).
            var bgIndex = index < _frames.Count - 1 ? index + 1 : index;
            var bg = _frames[bgIndex];
            OffsetX = bg.OffsetX;
            OffsetY = bg.OffsetY;
            Rotation = bg.Rotation;
            Scale = bg.Scale;
        }
        else
        {
            OffsetX = 0;
            OffsetY = 0;
            Rotation = 0;
            Scale = 1;
        }
        CurrentIndex = index;
    }
}
