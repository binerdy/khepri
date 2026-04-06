// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Khepri.Application.Timelapse;
using Khepri.Domain.Timelapse;

namespace Khepri.Presentation.Timelapse;

/// <summary>A thumbnail entry in the alignment filmstrip.</summary>
public sealed record FilmstripItem(string FilePath, bool IsActive);

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
[QueryProperty(nameof(RawProjectId), "projectId")]
public sealed partial class FrameAlignViewModel(
    TimelapseService timelapseService,
    FrameAlignService alignService) : ObservableObject
{
    private Guid _projectId;
    private List<TimelapseFrame>? _frames;

    // ─── Query property ───────────────────────────────────────────────────────

    public string RawProjectId
    {
        set
        {
            _projectId = Guid.Parse(value);
            _ = LoadAsync();
        }
    }

    // ─── Observable state ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundPath))]
    [NotifyPropertyChangedFor(nameof(GhostPath))]
    [NotifyPropertyChangedFor(nameof(GhostOffsetX))]
    [NotifyPropertyChangedFor(nameof(GhostOffsetY))]
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

    /// <summary>Opacity for the ghost (next-frame) overlay. Range 0.1 – 0.9.</summary>
    [ObservableProperty] public partial double GhostOpacity { get; set; } = 0.4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; private set; }

    // ─── Derived properties ───────────────────────────────────────────────────

    /// <summary>Bottom layer — the frame being repositioned (opaque, draggable).</summary>
    public string? BackgroundPath => (_frames is { Count: > 0 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count)
        ? _frames[Math.Min(CurrentIndex + 1, _frames.Count - 1)].FilePath
        : null;

    /// <summary>Top layer — the already-aligned reference frame (transparent, fixed).</summary>
    public string? GhostPath => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].FilePath
        : null;

    /// <summary>Playback offset of the ghost (reference) frame so it renders at its saved position.</summary>
    public double GhostOffsetX => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].OffsetX
        : 0;

    /// <inheritdoc cref="GhostOffsetX"/>
    public double GhostOffsetY => (_frames is { Count: > 1 } && CurrentIndex >= 0 && CurrentIndex < _frames.Count - 1)
        ? _frames[CurrentIndex].OffsetY
        : 0;

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
                .Select((f, i) => new FilmstripItem(f.FilePath, i == activeIdx))
                .ToList();
        }
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        var all     = await timelapseService.GetAllProjectsAsync();
        var project = all.FirstOrDefault(p => p.Id == _projectId);
        if (project is null)
        {
            return;
        }

        _frames    = project.Frames.OrderBy(f => f.Index).ToList();
        FrameCount = _frames.Count;
        SetIndex(0);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Resets the current frame's offset back to zero in memory (not persisted).</summary>
    [RelayCommand]
    private void Reset()
    {
        OffsetX = 0;
        OffsetY = 0;
    }

    [RelayCommand]
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

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_frames is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // At the last (preview-only) step there is no frame pair to save.
            if (CurrentIndex >= _frames.Count - 1)
            {
                return;
            }

            // Background = frame[CurrentIndex + 1] — the frame being repositioned.
            var frame = _frames[CurrentIndex + 1];
            await alignService.SaveAlignmentAsync(
                _projectId, frame.Id, OffsetX, OffsetY, cancellationToken);
            // Keep in-memory list in sync so PREV/NEXT and GhostOffsetX/Y reflect saved values.
            frame.SetOffset(OffsetX, OffsetY);
        }
        catch (OperationCanceledException)
        {
            // Navigation cancelled — nothing to surface.
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetIndex(int index)
    {
        if (_frames is not null && index < _frames.Count)
        {
            // Load the saved offset of the frame that will be displayed as the background.
            // At normal alignment steps: background is frame[index + 1].
            // At the last preview-only step: background is frame[index] (the final frame).
            var bgIndex = index < _frames.Count - 1 ? index + 1 : index;
            OffsetX = _frames[bgIndex].OffsetX;
            OffsetY = _frames[bgIndex].OffsetY;
        }
        else
        {
            OffsetX = 0;
            OffsetY = 0;
        }
        CurrentIndex = index;
    }
}
