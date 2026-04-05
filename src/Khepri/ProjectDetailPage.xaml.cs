// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Khepri.Presentation.Timelapse;

namespace Khepri;

public partial class ProjectDetailPage : ContentPage
{
    private readonly ProjectDetailViewModel _vm;
    private FrameDisplayItem? _dragSource;

    public ProjectDetailPage(ProjectDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var current = _vm.Project?.Name ?? string.Empty;
        var newName = await DisplayPromptAsync(
            "Rename Project",
            "Enter a new name:",
            "RENAME",
            "CANCEL",
            initialValue: current);

        if (!string.IsNullOrWhiteSpace(newName) && newName != current)
        {
            await _vm.RenameCommand.ExecuteAsync(newName);
        }
    }

    private async void OnPlayClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync($"TimelapsePreview?projectId={_vm.CurrentProjectId}");

    private void OnEnterSelectModeClicked(object? sender, EventArgs e)
    {
        foreach (var item in _vm.DisplayFrames)
        {
            item.IsSelected = false;
        }

        _vm.EnterSelectModeCommand.Execute(null);
    }

    private void OnExitSelectModeClicked(object? sender, EventArgs e)
    {
        foreach (var item in _vm.DisplayFrames)
        {
            item.IsSelected = false;
        }

        _vm.ExitSelectModeCommand.Execute(null);
    }

    private async void OnDeleteSelectedClicked(object? sender, EventArgs e)
    {
        var items = _vm.DisplayFrames.Where(f => f.IsSelected).ToList();
        var count = items.Count;
        if (count == 0)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            $"Delete {count} Frame{(count == 1 ? "" : "s")}",
            "This cannot be undone.",
            "DELETE",
            "CANCEL");

        if (!confirmed)
        {
            return;
        }

        OnExitSelectModeClicked(null, EventArgs.Empty);
        await _vm.DeleteSelectedFramesAsync(items);
    }

    private void OnFrameTapped(object? sender, TappedEventArgs e)
    {
        if (!_vm.IsSelecting)
        {
            return;
        }

        if (sender is Element { BindingContext: FrameDisplayItem item })
        {
            item.IsSelected = !item.IsSelected;
            _vm.SelectedCount = _vm.DisplayFrames.Count(f => f.IsSelected);
        }
    }

    private void OnFrameDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (_vm.IsSelecting)
        {
            e.Cancel = true;
            return;
        }

        if (sender is Element { BindingContext: FrameDisplayItem item })
        {
            _dragSource = item;
        }
    }

    private async void OnFrameDrop(object? sender, DropEventArgs e)
    {
        if (_dragSource is null)
            return;

        if (sender is Element { BindingContext: FrameDisplayItem target } && target != _dragSource)
        {
            await _vm.MoveFrameAsync(_dragSource.Frame, target.Frame);
        }

        _dragSource = null;
    }
}
