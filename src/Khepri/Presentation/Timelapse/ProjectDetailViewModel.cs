// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
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

    // Exposed for Shell navigation from the code-behind.
    public Guid CurrentProjectId => _projectId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFrames))]
    public partial TimelapseProject? Project { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelecting))]
    public partial bool IsSelecting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial int SelectedCount { get; set; }

    public bool IsNotSelecting => !IsSelecting;
    public bool HasSelection   => SelectedCount > 0;
    public bool HasFrames      => Project?.Frames.Count > 0;

    public ObservableCollection<FrameDisplayItem> DisplayFrames { get; } = [];

    [RelayCommand]
    private void EnterSelectMode()
    {
        foreach (var item in DisplayFrames)
        {
            item.IsSelected = false;
        }

        IsSelecting = true;
        SelectedCount = 0;
    }

    [RelayCommand]
    private void ExitSelectMode()
    {
        foreach (var item in DisplayFrames)
        {
            item.IsSelected = false;
        }

        IsSelecting = false;
        SelectedCount = 0;
    }

    public async Task DeleteSelectedFramesAsync(IEnumerable<FrameDisplayItem> items, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            foreach (var item in items.ToList())
            {
                await timelapseService.DeleteFrameAsync(_projectId, item.Frame.Id, cancellationToken);
            }

            await LoadAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task MoveFrameAsync(TimelapseFrame source, TimelapseFrame target, CancellationToken cancellationToken = default)
    {
        // Find the target's current position in DisplayFrames.
        var targetItem = DisplayFrames.FirstOrDefault(d => d.Frame.Id == target.Id);
        if (targetItem is null)
        {
            return;
        }

        var toPosition = targetItem.Position;
        await timelapseService.MoveFrameAsync(_projectId, source.Id, toPosition, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var all = await timelapseService.GetAllProjectsAsync(cancellationToken);
            Project = all.FirstOrDefault(p => p.Id == _projectId);

            DisplayFrames.Clear();
            if (Project is not null)
            {
                // Iterate in reverse so the newest frame (highest index) appears first in the list.
                for (var i = Project.Frames.Count - 1; i >= 0; i--)
                {
                    DisplayFrames.Add(new FrameDisplayItem(Project.Frames[i], i));
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RenameAsync(string newName, CancellationToken cancellationToken)
    {
        await timelapseService.RenameProjectAsync(_projectId, newName, cancellationToken);
        await LoadAsync(cancellationToken);
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

